using EvershadeEditor.LM2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;

namespace AnarkBrowser
{
    public class ScriptFormat
    {
        // Structures de données
        public class Script
        {
            public uint Hash { get; set; }
            public uint StringBufferSize { get; set; }
            public List<Function> Functions { get; set; } = new List<Function>();
        }

        public class Function
        {
            public string Name { get; set; }
            public uint Hash { get; set; }
            public uint CodeStartIndex { get; set; }
            public uint Flag { get; set; }

            public List<Operation> Operations { get; set; } = new List<Operation>();
            public Dictionary<uint, CodeVariable> Variables { get; set; } = new Dictionary<uint, CodeVariable>();

            // Pour l'affichage
            public string DecompiledCode { get; set; }
        }

        public class Operation
        {
            public ushort RawCode;
            public OperationCode OpCode;
            public uint RegValue;
            public uint RegValueEx;
            public object DataRead; // Valeur lue (float, int, string...)
        }

        public class CodeVariable
        {
            public uint Offset;
            public object Data; // float, uint, Vector3, etc.
            public string TypeName => Data?.GetType().Name ?? "Unknown";
        }

        public enum OperationCode
        {
            READ = 0,
            READ_U24 = 1,
            STRING_OFFSET = 2,
            STRING_OFFSETU24 = 3,
            SET = 4,
            JUMP1 = 5,
            JUMP2 = 6,
            CMD = 7,
            CMD_U24 = 8,
            NOOP = 0xA,
            RUN = 0xB,
            SET_FUNC_HASH = 0x14,
            END = 0xC,
            PTR = 0xE,
            MOV_8 = 0x10,
            MOV_4 = 0x11,
            SHIFT_PTR = 0x15,
            SHIFT_PTR_U24 = 0x16,
            MAX = 0x27,
        }

        public enum CommandRun
        {
            VEC3 = 824,
            VEC4 = 821,
            LIST_ADD = 434,
            LIST_ITEM = 21,
            BOOL = 57,
            MATRIX4X4 = 560,
        }

        // Propriétés principales
        public List<Script> Scripts { get; private set; } = new List<Script>();
        public List<string> Strings { get; private set; } = new List<string>();
        public List<dynamic> DataValues { get; private set; } = new List<dynamic>(); // Le pool de données
        public uint HashType { get; private set; }

        private ChunkEntry _rootChunk;

        public ScriptFormat(ChunkEntry rootChunk)
        {
            _rootChunk = rootChunk;
            Load();
        }

        private void Load()
        {
            if (_rootChunk.Children == null) return;

            // 1. Récupération des chunks enfants
            var headerChunk = _rootChunk.Children.FirstOrDefault(c => c.Type == (ushort)ChunkType.ScriptHeader);
            var funcTableChunks = _rootChunk.Children.Where(c => c.Type == (ushort)ChunkType.ScriptFunctionTable).ToList();
            var dataChunk = _rootChunk.Children.FirstOrDefault(c => c.Type == (ushort)ChunkType.ScriptData);

            if (headerChunk == null || dataChunk == null) throw new Exception("Structure de script invalide (Header ou Data manquants)");

            // 2. Parsing du Header (Liste des scripts)
            using (var reader = new BinaryReader(new MemoryStream(headerChunk.Data)))
            {
                int numScripts = headerChunk.Data.Length / 8;
                for (int i = 0; i < numScripts; i++)
                {
                    Scripts.Add(new Script
                    {
                        Hash = reader.ReadUInt32(),
                        StringBufferSize = reader.ReadUInt32()
                    });
                }
            }

            // 3. Parsing des Tables de Fonctions
            for (int i = 0; i < Scripts.Count; i++)
            {
                if (i >= funcTableChunks.Count) break;

                using (var reader = new BinaryReader(new MemoryStream(funcTableChunks[i].Data)))
                {
                    int numFuncs = funcTableChunks[i].Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        var func = new Function
                        {
                            Hash = reader.ReadUInt32(),
                            CodeStartIndex = reader.ReadUInt32(),
                            Flag = reader.ReadUInt32()
                        };
                        func.Name = Helper.GetHashName(func.Hash);
                        if (string.IsNullOrWhiteSpace(func.Name)) func.Name = $"Func_{func.Hash:X8}";

                        Scripts[i].Functions.Add(func);
                    }
                }
            }

            // 4. Parsing des Données et du Code (Le gros morceau)
            ParseScriptData(dataChunk.Data);
        }

        private void ParseScriptData(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                HashType = reader.ReadUInt32(); // "COGScriptInterpreter" hash usually
                uint codeSize = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                ushort stringTableSize = reader.ReadUInt16();
                ushort unk = reader.ReadUInt16();

                long dataStartPos = reader.BaseStream.Position;
                long codeStartPos = dataStartPos + dataSize;
                long stringStartPos = codeStartPos + codeSize;

                // A. Charger les Strings
                reader.BaseStream.Seek(stringStartPos, SeekOrigin.Begin);
                while (reader.BaseStream.Position < reader.BaseStream.Length && reader.BaseStream.Position < stringStartPos + stringTableSize)
                {
                    // Lecture sécurisée des strings
                    try { Strings.Add(reader.ReadZeroTerminatedString()); } catch { break; }
                }

                // B. Analyser chaque fonction
                foreach (var script in Scripts)
                {
                    foreach (var func in script.Functions)
                    {
                        ParseFunction(reader, func, codeStartPos, dataStartPos, dataSize, stringStartPos);
                    }
                }
            }
        }

        private void ParseFunction(BinaryReader reader, Function func, long codeBase, long dataBase, uint maxDataSize, long strBase)
        {
            reader.BaseStream.Seek(codeBase + func.CodeStartIndex * 2, SeekOrigin.Begin);

            uint dataPointer = 0;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"// Function: {func.Name}");
            sb.AppendLine("{");

            List<dynamic> stack = new List<dynamic>();

            while (true)
            {
                ushort raw = reader.ReadUInt16();
                uint val = (uint)(raw & 0xffff03ff);
                uint opCode = (uint)(raw >> 10);

                var op = new Operation { RawCode = raw, OpCode = (OperationCode)opCode, RegValue = val };
                func.Operations.Add(op);

                // Helper local pour lire une valeur
                dynamic GetDataValue(uint index)
                {
                    long oldPos = reader.BaseStream.Position;
                    reader.BaseStream.Seek(dataBase + index * 4, SeekOrigin.Begin);

                    uint rawVal = reader.ReadUInt32();
                    // Heuristique simple pour distinguer float/int (comme dans l'original)
                    float fVal = BitConverter.ToSingle(BitConverter.GetBytes(rawVal), 0);

                    reader.BaseStream.Seek(oldPos, SeekOrigin.Begin);

                    // Si c'est un hash connu ou un int "propre", on garde l'int, sinon float
                    if (Helper.Hashes.ContainsKey(rawVal) || fVal > 100000 || fVal < -100000) return rawVal;
                    return fVal;
                }

                string debugLine = $"    [{op.OpCode}] Val:{val}";

                switch (op.OpCode)
                {
                    case OperationCode.READ:
                        if (val < maxDataSize / 4)
                        {
                            var v = GetDataValue(val);
                            op.DataRead = v;
                            stack.Add(v);
                            debugLine += $" -> Read: {v}";
                        }
                        break;

                    case OperationCode.STRING_OFFSET:
                        // Récupération string
                        long oldP = reader.BaseStream.Position;
                        reader.BaseStream.Seek(strBase + val, SeekOrigin.Begin);
                        string s = reader.ReadZeroTerminatedString();
                        reader.BaseStream.Seek(oldP, SeekOrigin.Begin);
                        op.DataRead = s;
                        stack.Add(s);
                        debugLine += $" -> String: \"{s}\"";
                        break;

                    case OperationCode.SET:
                        stack.Add(val);
                        break;

                    case OperationCode.PTR:
                        dataPointer = val * 4 + 4;
                        break;

                    case OperationCode.SHIFT_PTR:
                        dataPointer += val;
                        break;

                    case OperationCode.MOV_8:
                        if (stack.Count > 0)
                        {
                            var valToStore = stack.Last();
                            // Stocker la variable pour l'éditeur
                            if (!func.Variables.ContainsKey(dataPointer))
                            {
                                func.Variables[dataPointer] = new CodeVariable { Offset = dataPointer, Data = valToStore };
                            }
                            sb.AppendLine($"    var_{dataPointer:X} = {valToStore};");
                            stack.Clear();
                        }
                        break;

                    case OperationCode.CMD:
                        // Gestion des commandes complexes (Vec3, etc.)
                        if ((CommandRun)val == CommandRun.VEC3 && stack.Count >= 3)
                        {
                            var v3 = new Vector3((float)stack[0], (float)stack[1], (float)stack[2]);
                            if (!func.Variables.ContainsKey(dataPointer))
                                func.Variables[dataPointer] = new CodeVariable { Offset = dataPointer, Data = v3 };
                            sb.AppendLine($"    vec3 var_{dataPointer:X} = {v3};");
                        }
                        else if ((CommandRun)val == CommandRun.BOOL && stack.Count >= 1)
                        {
                            bool b = ((dynamic)stack[0] == 1);
                            if (!func.Variables.ContainsKey(dataPointer))
                                func.Variables[dataPointer] = new CodeVariable { Offset = dataPointer, Data = b };
                            sb.AppendLine($"    bool var_{dataPointer:X} = {b};");
                        }
                        else
                        {
                            sb.AppendLine($"    CMD_{(CommandRun)val}({string.Join(", ", stack)});");
                        }
                        stack.Clear();
                        break;

                    case OperationCode.END:
                        sb.AppendLine("}");
                        func.DecompiledCode = sb.ToString();
                        return; // Fin de fonction

                    default:
                        // Gestion des extensions (lectures 2 bytes supplémentaires)
                        if (new[] { 1, 3, 5, 6, 8, 0x16 }.Contains((int)opCode))
                        {
                            op.RegValueEx = reader.ReadUInt16();
                        }
                        break;
                }

                // sb.AppendLine(debugLine); // Uncomment for raw debug
            }
        }

        // Sauvegarde simplifiée : met à jour le DATA chunk avec les nouvelles variables
        // Attention : on ne recompile pas le bytecode, on met juste à jour les valeurs pointées par READ
        public void SaveVariables()
        {
            var dataChunk = _rootChunk.Children.FirstOrDefault(c => c.Type == (ushort)ChunkType.ScriptData);
            if (dataChunk == null) return;

            using (var ms = new MemoryStream(dataChunk.Data))
            using (var writer = new BinaryWriter(ms))
            using (var reader = new BinaryReader(ms))
            {
                // Lire le header pour trouver l'offset des data
                reader.BaseStream.Seek(4, SeekOrigin.Begin); // Skip HashType
                uint codeSize = reader.ReadUInt32();
                uint dataSize = reader.ReadUInt32();
                // Data commence à 16 (Header size)
                long dataStart = 16;

                // Parcourir toutes les variables modifiées de toutes les fonctions
                foreach (var script in Scripts)
                {
                    foreach (var func in script.Functions)
                    {
                        foreach (var op in func.Operations)
                        {
                            // On cherche les opérations READ qui chargent des données
                            if (op.OpCode == OperationCode.READ && op.DataRead != null)
                            {
                                // A-t-on une variable qui correspond à cette valeur ?
                                // C'est une approche simpliste : on cherche dans les variables si la valeur "Originale" match
                                // Dans une vraie implémentation, il faudrait mapper Variable -> Index Data
                                // Pour cet exemple, on suppose que l'utilisateur édite les variables détectées par MOV_8/CMD

                                // TODO: Une implémentation d'écriture robuste nécessite de reconstruire tout le blob Data.
                                // Ici, c'est risqué d'écrire directement sans mapper l'index exact.
                            }
                        }
                    }
                }
            }
        }

        // Méthode de secours pour update spécifique via l'UI
        public void UpdateVariableInRawData(uint offsetInDataValues, dynamic newValue)
        {
            // Cette méthode devrait être appelée par l'éditeur si on mappait l'offset de lecture
            // Pour l'instant, l'édition est complexe sans recompiler tout le script.
        }
    }
}