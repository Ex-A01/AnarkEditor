using CtrLibrary.Bcres;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Syroot.BinaryData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Xml.Serialization;
using Toolbox.Core.GUI;
using Toolbox.Core.IO;
using static NextLevelLibrary.LM2.ScriptFormat;
using static NextLevelLibrary.LM2.ScriptFormat.Function;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace NextLevelLibrary.LM2
{
    /// <summary>
    /// Represents a scripting format for handling ASM scripting data.
    /// </summary>
    public class ScriptFormat : IFormat
    {
        /// <summary>
        /// Represents a sub script in the script file.
        /// </summary>
        public class Script
        {
            /// <summary>
            /// The name hash of the script.
            /// </summary>
            public uint Hash { get; set; }

            /// <summary>
            /// Some buffer size related to a hash table.
            /// </summary>
            public uint StringBufferSize { get; set; }

            /// <summary>
            /// The script functions for exeucting code.
            /// </summary>
            public List<Function> Functions = new List<Function>();
        }


        public class Function
        {
            public string Name;
            public uint Hash;

            public uint CodeStartIndex;
            public uint Flag;

            [XmlIgnore]
            public Dictionary<uint, CodeVariable> Variables = new Dictionary<uint, CodeVariable>();

            [XmlIgnore]
            public List<Operation> Operations = new List<Operation>();

            [XmlIgnore]
            public List<CodeValue> Code = new List<CodeValue>();

            public void AddVariableString(uint offset, dynamic data)
            {
                var var = new CodeVariable(offset, data);

                if (!Variables.ContainsKey(offset))
                    Variables.Add(offset, var);

                //Also add it to code list to keep track of code order
                Code.Add(var);
            }

            public void AddVariableCmd(uint offset, dynamic data)
            {
                var var = new CodeVariable(offset, data);

                if (!Variables.ContainsKey(offset))
                    Variables.Add(offset, var);

                //Also add it to code list to keep track of code order
                Code.Add(var);
            }

            public void AddUnkCmd(uint offset, dynamic data, uint regType)
            {
                Code.Add(new CodeVariable(offset, data, regType));
            }

            public void AddCmd(uint opCode, uint value)
            {
                Code.Add(new CodeValue(opCode, value));
            }

            //Reload the operations from added code values
            public void GenerateCode()
            {
                File.WriteAllText("codeOG.json", JsonConvert.SerializeObject(Operations, Formatting.Indented));

                Operations.Clear();
                foreach (var code in this.Code)
                {
                    if (code is CodeVariable)
                    {
                        //Note : read values update indices on save 
                        var variable = (CodeVariable)code;

                        AddOperation(OperationCode.PTR, 0);
                        AddOperation(OperationCode.SHIFT_PTR, variable.Offset - 4);

                        if (variable.Data is Vector3)
                        {
                            var vec3 = (Vector3)variable.Data;
                            AddOperationValue(OperationCode.READ, vec3.X);
                            AddOperationValue(OperationCode.READ, vec3.Y);
                            AddOperationValue(OperationCode.READ, vec3.Z);
                            AddOperation(OperationCode.CMD, (uint)CommandRun.VEC3);
                        }
                        else if (variable.Data is Vector4)
                        {
                            var vec = (Vector4)variable.Data;
                            AddOperationValue(OperationCode.READ, vec.X);
                            AddOperationValue(OperationCode.READ, vec.Y);
                            AddOperationValue(OperationCode.READ, vec.Z);
                            AddOperationValue(OperationCode.READ, vec.Z);
                            AddOperation(OperationCode.CMD, (uint)CommandRun.VEC4);
                        }
                        else if (variable.Data is float[])
                        {
                            var f = (float[])variable.Data;
                            for (int i = 0; i < 16; i++)
                            {
                                if (f[i] == 0)
                                    AddOperation(OperationCode.SET, 0);
                                else
                                    AddOperationValue(OperationCode.READ, f[i]);
                            }
                            AddOperation(OperationCode.CMD, (uint)CommandRun.MATRIX4X4);
                        }
                        else if (variable.Data is bool)
                        {
                            int val = ((bool)variable.Data ? 0 : 1);
                            AddOperation(OperationCode.SET, (uint)val);
                            AddOperation(OperationCode.CMD, (uint)CommandRun.BOOL);
                        }
                        else if (variable.Data is float)
                        {
                            AddOperationValue(OperationCode.READ, (float)variable.Data);
                            AddOperation(OperationCode.MOV_8, 4);
                        }
                        else if (variable.Data is short)
                        {
                            AddOperation(OperationCode.SET, (uint)((short)variable.Data));
                            AddOperation(OperationCode.MOV_8, 2);
                        }
                        else if (variable.Data is byte)
                        {
                            AddOperation(OperationCode.SET, (uint)((byte)variable.Data));
                            AddOperation(OperationCode.MOV_8, 1);
                        }
                        else if (variable.Data is uint)
                        {
                            if ((uint)variable.Data > 1000)
                                AddOperationValue(OperationCode.READ, (uint)variable.Data);
                            else
                                AddOperation(OperationCode.SET, (uint)variable.Data);
                            AddOperation(OperationCode.MOV_8, 4);
                        }
                        else if (variable.Data is IEnumerable<dynamic>)
                        {
                            foreach (var value in variable.Data)
                            {
                                if (value is float)
                                {
                                    AddOperationValue(OperationCode.READ, (float)value.Data);
                                }
                                else if (value is uint)
                                {
                                    if ((uint)value > 1000)
                                        AddOperationValue(OperationCode.READ, (uint)value.Data);
                                    else
                                        AddOperation(OperationCode.SET, (uint)value.Data);
                                }
                                else if (value is short)
                                    AddOperation(OperationCode.SET, (uint)((short)value.Data));
                                else if (value is byte)
                                    AddOperation(OperationCode.SET, (uint)((byte)value.Data));
                            }
                            AddOperation(OperationCode.CMD, variable.RegValue);
                        }
                        else if (variable.Data == null)
                        {
                            AddOperation(OperationCode.CMD, variable.RegValue);
                        }
                        else
                            throw new Exception($"Unknown data type! {variable.Data}");
                    }
                    else
                    {
                        AddOperation((OperationCode)code.OpCode, code.RegValue);
                    }
                }
                AddOperationEnd();

                File.WriteAllText("codeNEW.json", JsonConvert.SerializeObject(Operations, Formatting.Indented));
            }

            public void AddOperation(OperationCode op, uint value)
            {
                Operations.Add(new Operation()
                {
                    RegValue = value,
                    OpCode = op,
                });
            }

            public void AddOperationValue(OperationCode op, dynamic readValue)
            {
                Operations.Add(new Operation()
                {
                    OpCode = op,
                    DataRead = readValue,
                });
            }

            public void AddOperationEnd()
            {
                Operations.Add(new Operation()
                {
                    OpCode = OperationCode.END, RegValue = 0,
                });
            }

            public List<dynamic> GetList(uint pointer)
            {
                if (!Variables.ContainsKey(pointer))
                    Variables.Add(pointer, new CodeVariable(pointer, new List<dynamic>()));
                return (List<dynamic>)Variables[pointer].Data;
            }

            public class CodeValue
            {
                public uint OpCode;
                public uint RegValue;

                public CodeValue() { }

                public CodeValue(uint opCode, uint value)
                {
                    OpCode = opCode;
                    RegValue = value;
                }

                public override string ToString()
                {
                    return $"   OpCode {(ScriptFormat.OperationCode)OpCode} Reg {RegValue}";
                }

            }

            public class CodeVariable : CodeValue
            {
                public uint Offset;
                public dynamic Data;
                public CodeVariable(uint offset, dynamic data, uint type = 0)
                {
                    RegValue = type;
                    Offset = offset;
                    Data = data;
                }

                public override string ToString()
                {
                    string varName = $"var{Offset:X}";
                    if (Data is Vector3 vec3)
                        return $"   Vec3 {varName} = Vec3({vec3.X}, {vec3.Y}, {vec3.Z})";
                    else if (Data is Vector4 vec4)
                        return $"   Vec4 {varName} = Vec4({vec4.X}, {vec4.Y}, {vec4.Z}, {vec4.W})";
                    else if (Data is float f)
                        return $"   float {varName} = {f}";
                    else if (Data is uint u)
                        return $"   uint {varName} = 0x{u:X}";
                    else if (Data is bool b)
                        return $"   bool {varName} = {(b ? "true" : "false")}";
                    else if (Data is float[] mat && mat.Length == 16)
                        return $"   Matrix4x4 {varName} = [{string.Join(", ", mat)}]";
                    else
                        return $"   {varName} = {Data}";
                }
            }

            public enum CmdType
            {
                Variable,
            }

            public enum VariableType
            {
                BYTE,
                SHORT,
                FLOAT,
                VEC3,
                VEC4,
                MATRIX,
            }
        }

        public class Operation
        {
            public OperationCode OpCode;
            public uint RegValue;
            public uint RegValueEx;
            public dynamic DataRead = null;
            public string String = "";
            public ushort Code;
        }

        public class StringHash
        {
            public uint Hash;
            public uint Unknown;
            public uint Offset;
        }

        public List<Script> Scripts = new List<Script>();

        public List<StringHash> StringHashes = new List<StringHash>();

        public uint[] HashTable = new uint[0];

        public uint HashType;

        /// <summary>
        /// The raw data of the script. Can be a floating value or a uint (hash)
        /// </summary>
        public float[] Data;

        /// <summary>
        /// 
        /// </summary>
        public List<string> Strings = new List<string>();

        [XmlIgnore]
        public ushort[] Code;

        public string ToCode()
        {
            StringBuilder sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
            {
                writer.WriteLine($"Script Type {Hashing.GetString(HashType)}");

                writer.Write($"Hash Table: [");
                foreach (var hash in this.HashTable)
                {
                    if (hash > 100)
                        writer.Write($" {Hashing.GetString(hash)} ");
                }
                writer.WriteLine($"]");

                writer.Write($"String Table: [");
                foreach (var str in this.Strings)
                    writer.Write($"{str}");

                writer.WriteLine($"]");

                writer.WriteLine($"String Hashes: [");
                foreach (var str in this.StringHashes)
                    writer.WriteLine($"{str.Offset} {Hashing.GetString(str.Hash)}");

                writer.WriteLine($"]");

                

                writer.WriteLine("");
                foreach (var script in this.Scripts)
                {
                    writer.WriteLine($"Script Hash {Hashing.GetString(script.Hash)}");
                    writer.WriteLine($"Script String Hash BufferSize: {script.StringBufferSize}");

                    writer.WriteLine("");
                    foreach (var func in script.Functions)
                    {
                        writer.WriteLine($"{func.Name}_{func.Flag.ToString("X")}()");
                        writer.WriteLine("{");

                        /*foreach (var var in func.Operations)
                        {
                            writer.WriteLine(var.Code.ToString("X"));
                        }*/

                            long pointer = 0;

                        List<dynamic> values = new List<dynamic>();
                        foreach (var var in func.Operations)
                        {
                            switch (var.OpCode)
                            {
                                case OperationCode.PTR:
                                    pointer = var.RegValue * 4 + 4;
                                    break;
                                case OperationCode.SHIFT_PTR:
                                    pointer += var.RegValue;
                                    break;
                                case OperationCode.READ:
                                    values.Add(var.DataRead);
                                    break;
                                case OperationCode.SET:
                                    values.Add(var.RegValue);
                                    break;
                                case OperationCode.STRING_OFFSET:
                                case OperationCode.STRING_OFFSETU24:
                                    writer.WriteLine($"   string var{pointer} = {var.String}");
                                    break;
                                case OperationCode.CMD:

                                    string variable = $"var{pointer}"; 

                                    switch ((CommandRun)var.RegValue)
                                    {
                                        case CommandRun.BOOL: 
                                            writer.WriteLine($"   bool {variable} = {((uint)values[0] == 1 ? "true" : "false")}"); 
                                            break;
                                        case CommandRun.LIST_ITEM: 
                                            writer.WriteLine($"item"); 
                                            break;
                                        case CommandRun.LIST_ADD:
                                            writer.WriteLine($"   list{pointer}.add({string.Join(',', values)})"); 
                                            break;
                                        case CommandRun.VEC3: 
                                            if (values.Count == 3)
                                                writer.WriteLine($"   Vec3 {variable} = Vec3({values[0]}, {values[1]}, {values[2]})"); break;
                                        case CommandRun.VEC4:
                                            if (values.Count == 4)
                                                writer.WriteLine($"   Vec4 {variable} = Vec4({values[0]}, {values[1]}, {values[2]}, {values[3]})"); break;
                                        case CommandRun.MATRIX4X4:
                                            if (values.Count == 16)
                                            {
                                                writer.WriteLine($"   Matrix[0] {variable} = Vec4({values[0]}, {values[1]}, {values[2]}, {values[3]})");
                                                writer.WriteLine($"   Matrix[1] {variable} = Vec4({values[4]}, {values[5]}, {values[6]}, {values[7]})");
                                                writer.WriteLine($"   Matrix[2] {variable} = Vec4({values[8]}, {values[9]}, {values[10]}, {values[11]})");
                                                writer.WriteLine($"   Matrix[3] {variable} = Vec4({values[12]}, {values[13]}, {values[14]}, {values[15]})");
                                            }
                                            break;
                                        default:
                                            writer.WriteLine($"   CMD {var.RegValue}");
                                            writer.WriteLine($"   DATA {string.Join(',', values)}");
                                            break;
                                    }

                                    values = new List<dynamic>();

                                    break;
                                case OperationCode.MOV_8:
                                    if (values.Count == 0)
                                    {
                                        break;
                                    }
                                    switch (var.RegValue)
                                    {
                                        case 4:
                                            if (values[0] is uint)
                                            {
                                                var v = (uint)values[0];
                                                if (Hashing.HashStrings.ContainsKey(v))
                                                    writer.WriteLine($"   hash var{pointer} = {Hashing.GetString(v)}");
                                                else
                                                    writer.WriteLine($"   uint var{pointer} = {string.Join(',', values)}");
                                            }
                                            else
                                                writer.WriteLine($"   float var{pointer} = {string.Join(',', values)}");
                                            break;
                                        case 2:
                                            writer.WriteLine($"   short var{pointer} = {string.Join(',', values)}");
                                            break;
                                        case 1:
                                            writer.WriteLine($"   byte var{pointer} = {string.Join(',', values)}");
                                            break;
                                        default:
                                            writer.WriteLine($"   byte[] var{pointer} = {string.Join(',', values)}");
                                            break;
                                    }
                                    values = new List<dynamic>();
                                    break;
                                default:
                                    if (var.RegValueEx != 0)
                                        writer.WriteLine($"   OpCode {var.OpCode} Reg {var.RegValueEx}");
                                    else
                                        writer.WriteLine($"   OpCode {var.OpCode} Reg {var.RegValue}");
                                    break;
                            }
                        }

                        foreach (var var in func.Variables)
                        {
                            writer.WriteLine($"    var{var.Key.ToString("X2")} = {string.Join(',', var.Value)}");
                        }

                       foreach (var op in func.Code)
                        writer.WriteLine($"   {op}");
                        writer.WriteLine("}");
                        writer.WriteLine("");

                    }
                }
            }
            return sb.ToString();
        }

        public static bool IsCOGScript(ChunkEntry file)//file entry become chunkentry
        {
            var scriptData = file.GetChild(ChunkType.ScriptData);

            using (var reader = new BinaryDataReader(scriptData.Data, true))
            {
                return Hashing.GetString(reader.ReadUInt32()) == "COGScriptInterpreter";
            }
        }

        public ScriptFormat() { }


        public void ToXML(string filePath)
        {
            XmlSerializer ser = new XmlSerializer(typeof(ScriptFormat));

            TextWriter writer = new StreamWriter(filePath);
            ser.Serialize(writer, this);
            writer.Close();
        }

        static int parsedIndex = 0;

        public ScriptFormat(ChunkFileEntry chunkFile, string dict)
        {
            var chunkList = chunkFile.Children;

            //Get the script headers first
            var headerChunk = chunkFile.GetChild(ChunkType.ScriptHeader);
            var functionTables = chunkFile.GetChildList(ChunkType.ScriptFunctionTable);
            //A script file can have multple scripts inside
            //Check amount by dividing header length
            var numScripts = headerChunk.Data.Length / 8;

            //Parse headers
            using (var reader = new FileReader(headerChunk.Data, true))
            {
                for (int i = 0; i < numScripts; i++)
                {
                    Script script = new Script();
                    script.Hash = reader.ReadUInt32();
                    //0 unless string hash chunk is used
                    //Possibly the buffer size to allocate the string hashes, which place via offset
                    script.StringBufferSize = reader.ReadUInt32();
                    Scripts.Add(script);
                }
            }

            //
            var strHashChunk = chunkFile.GetChild(ChunkType.ScriptStringHashes);
            if (strHashChunk != null)
            {
                int index = 0;
                using (var reader = new FileReader(strHashChunk.Data, true))
                {
                    uint numFuncs = (uint)strHashChunk.Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        StringHash strHash = new StringHash();
                        strHash.Hash = reader.ReadUInt32();
                        strHash.Unknown = reader.ReadUInt32(); //5560CC24
                        strHash.Offset = reader.ReadUInt32();
                        StringHashes.Add(strHash);

                        //Console.WriteLine("Ext " + Hashing.CreateHashString(strHash.Hash));
                    }
                }
            }

            //Parse function table used for each script

            //This should not happen
            if (functionTables.Count != Scripts.Count)  //functionTables.Data.Length / 4 was functionTables.Count
                throw new Exception("Unexpected table count!");

            for (int i = 0; i < Scripts.Count; i++)
            {
                using (var reader = new FileReader(functionTables[i].Data, true))  //ok so wtf 
                {
                    uint numFuncs = (uint)functionTables[i].Data.Length / 12;
                    for (int j = 0; j < numFuncs; j++)
                    {
                        Function func = new Function();
                        func.Hash = reader.ReadUInt32();
                        func.Name = Hashing.GetString(func.Hash);
                        func.CodeStartIndex = reader.ReadUInt32();
                        func.Flag = reader.ReadUInt32(); 
                        Scripts[i].Functions.Add(func);

                        Console.WriteLine("FUNC " + func.Name);
                    }
                }
            }

            //Optional hash bundles
            var hashBundleChunk = chunkFile.GetChild(ChunkType.ScriptHashBundle);
            if (hashBundleChunk != null)
            {
                using (var reader = new FileReader(hashBundleChunk.Data, true))
                {
                    HashTable = reader.ReadUInt32s((int)hashBundleChunk.Data.Length / 4); 
                }
            }

            //Raw data
            var dataChunk = chunkFile.GetChild(ChunkType.ScriptData);
            try
            {
                ParseScriptData(new FileReader(dataChunk.Data, true), Scripts);
                EditMode(new FileReader(dataChunk.Data, true), Scripts, chunkFile);
            }
            catch
            {

            }
            if (!Directory.Exists(Path.Combine(AppContext.BaseDirectory, dict)))
            {
                Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, dict));
            }
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, dict, "ParsedScript" + parsedIndex + ".txt"), ToCode());
            //ToXML(Path.Combine(AppContext.BaseDirectory, "Script", "XMLScript" + parsedIndex + ".xml"));
            parsedIndex++;
        }

        public void Save(ChunkFileEntry chunkFile)
        {
            //Only save COG types for now
            if (Hashing.GetString(this.HashType) != "COGScriptInterpreter")
                return;

            ushort chunkFlags = 512;

            chunkFile.Children.Clear();

            //Note script data must be first. Use original order
            var scriptData = chunkFile.AddChild(ChunkType.ScriptData); //var scriptData = chunkFile.AddChild(ChunkType.ScriptData, chunkFlags);
            var scriptHeader = chunkFile.AddChild(ChunkType.ScriptHeader); //var scriptHeader = chunkFile.AddChild(ChunkType.ScriptHeader, chunkFlags);

            WriteScriptData(scriptData);

            using (var writer = new FileWriter(scriptHeader.Data, true))
            {
                foreach (var script in this.Scripts)
                {
                    writer.Write(script.Hash);
                    writer.Write(script.StringBufferSize);
                }
            }
            foreach (var script in this.Scripts)
            {
                var scriptFunctionTable = chunkFile.AddChild(ChunkType.ScriptFunctionTable); //                var scriptFunctionTable = chunkFile.AddChild(ChunkDataType.ScriptFunctionTable, chunkFlags);
                using (var writer = new FileWriter(scriptFunctionTable.Data, true))
                {
                    foreach (var func in script.Functions)
                    {
                        writer.Write(func.Hash);
                        writer.Write(func.CodeStartIndex);
                        writer.Write(func.Flag);
                    }
                }
            }

        }

        private void WriteScriptData(ChunkEntry chunk)
        {
            var codeBuffer = new MemoryStream();
            var stringBuffer = new MemoryStream();

            List<dynamic> data = new List<dynamic>();

            using (var writer = new FileWriter(codeBuffer, true))
            {
                foreach (var function in Scripts.SelectMany(x => x.Functions))
                {
                    Dictionary<uint, int> counters = new Dictionary<uint, int>();

                    //Read the data buffer and update with the current values
                    function.CodeStartIndex = (ushort)(writer.Position / 2);
                    //Write operation codes 
                    //Update the data buffer if READ operation is used
                    foreach (var op in function.Operations)
                    {
                        uint value = op.RegValue;

                        if (op.OpCode == OperationCode.READ && op.DataRead != null)
                        {
                            if (!data.Contains(op.DataRead))
                                data.Add(op.DataRead);
                            value = (uint)data.IndexOf(op.DataRead); //data read index
                        }

                        writer.Write((ushort)((int)value | (int)op.OpCode << 10));
                    }
                }
            }

            using (var writer = new FileWriter(stringBuffer, true))
            {
                foreach (var str in this.Strings)
                    writer.Write(str, BinaryStringFormat.ZeroTerminated);
            }

            //Save the data section
            byte[] code = codeBuffer.ToArray();
            byte[] strings = stringBuffer.ToArray();

            using (var writer = new FileWriter(chunk.Data, true))
            {
                writer.Write(this.HashType);
                writer.Write(code.Length); //codeSize 
                writer.Write(data.Count * 4); //dataSize 
                writer.Write((ushort)stringBuffer.Length); //stringTableSize
                writer.Write((ushort)0); //unk
                foreach (var val in data)
                    writer.Write(val);
                writer.Write(code);
                writer.Write(strings);
            }
        }

        void ParseScriptData(FileReader reader, List<Script> scripts)
        {
            HashType = reader.ReadUInt32(); //FlowScript or some unknown hash
            uint codeSize = reader.ReadUInt32();
            uint dataSize = reader.ReadUInt32();
            ushort stringTableSize = reader.ReadUInt16();
            ushort num = reader.ReadUInt16(); //unk. Usually 0

            var pos = reader.Position;
            Data = reader.ReadSingles((int)dataSize / 4);
            Code = reader.ReadUInt16s((int)codeSize / 2);

            var strPos = 16 + codeSize + dataSize;
            reader.SeekBegin(strPos);
            while (!reader.EndOfStream)
            {
                string str = reader.ReadZeroTerminatedString();
                Strings.Add(str);
            }

            reader.SeekBegin(pos + dataSize);

            ushort previousOp = 0;

            long codePos = reader.Position;
            foreach (var script in scripts)
            {
                for (int i = 0; i < script.Functions.Count; i++)
                    ReadFunction(reader, script.Functions[i], codePos, strPos, pos, dataSize);
            }

        }

        void EditMode(FileReader reader, List<Script> scripts, ChunkFileEntry chunkFileEntry)
        {
            // On vérifie qu'on est bien sur le bon type de script
            if (Hashing.GetString(HashType) == "COGScriptInterpreter")
            {
                while (true) // Boucle principale de l'éditeur
                {
                    Console.Clear();
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"=== ÉDITEUR DE SCRIPT : {Hashing.GetString(HashType)} ===");
                    Console.BackgroundColor = ConsoleColor.Black;

                    // Lister les fonctions disponibles
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    for (int i = 0; i < scripts.Count; i++)
                    {
                        for (int j = 0; j < scripts[i].Functions.Count; j++)
                        {
                            var func = scripts[i].Functions[j];
                            Console.WriteLine($"ID {j} : {func.Name} (Hash: {func.Hash:X})");
                        }
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nEntrez l'ID de la fonction à éditer (ou 'Q' pour quitter/sauvegarder) :");
                    string input = Console.ReadLine();

                    if (input.ToLower() == "q") break;

                    if (int.TryParse(input, out int funcIndex))
                    {
                        // Vérification basique (on suppose script[0] pour simplifier, à adapter si multi-scripts)
                        if (scripts.Count > 0 && funcIndex >= 0 && funcIndex < scripts[0].Functions.Count)
                        {
                            EditFunctionVariables(scripts[0].Functions[funcIndex]);
                        }
                    }
                }

                Console.WriteLine("Voulez-vous sauvegarder les modifications dans le chunk en mémoire ? (Y/N)");
                if (Console.ReadKey(true).Key == ConsoleKey.Y)
                {
                    Save(chunkFileEntry); // Reconstruit le chunk binaire avec les nouvelles valeurs
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Chunk mis à jour en mémoire !");
                }
            }
            Console.BackgroundColor = ConsoleColor.Black;
        }

        // Sous-méthode pour gérer l'édition des variables d'une fonction spécifique
        void EditFunctionVariables(Function func)
        {
            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"--- Variables de la fonction {func.Name} ---");

                var vars = func.Variables.Values.ToList();
                for (int i = 0; i < vars.Count; i++)
                {
                    var variable = vars[i];
                    string typeName = variable.Data != null ? variable.Data.GetType().Name : "null";
                    Console.WriteLine($"[{i}] Offset: {variable.Offset:X} | Type: {typeName} | Val: {variable.Data}");
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nEntrez l'index de la variable à modifier (ou 'R' pour retour) :");
                string input = Console.ReadLine();

                if (input.ToLower() == "r") break;

                if (int.TryParse(input, out int varIndex) && varIndex >= 0 && varIndex < vars.Count)
                {
                    var targetVar = vars[varIndex];
                    ModifyVariableValue(targetVar);
                }
            }
        }

        // Sous-méthode pour modifier la valeur selon le type
        void ModifyVariableValue(CodeVariable variable)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"\nModification de {variable.Data?.GetType().Name} (Actuel: {variable.Data})");

            switch (variable.Data)
            {
                case float f:
                    Console.Write("Nouvelle valeur (float) : ");
                    if (float.TryParse(Console.ReadLine(), out float newFloat)) variable.Data = newFloat;
                    break;

                case uint u:
                    Console.Write("Nouvelle valeur (uint/hash) : ");
                    // On accepte l'entrée hexadécimale ou décimale
                    string uInput = Console.ReadLine();
                    if (uInput.StartsWith("0x")) uInput = uInput.Substring(2);
                    if (uint.TryParse(uInput, System.Globalization.NumberStyles.HexNumber, null, out uint newUint)) variable.Data = newUint;
                    else if (uint.TryParse(uInput, out uint newUintDec)) variable.Data = newUintDec;
                    break;

                case int i:
                    Console.Write("Nouvelle valeur (int) : ");
                    if (int.TryParse(Console.ReadLine(), out int newInt)) variable.Data = newInt;
                    break;

                case Vector3 v3:
                    Console.WriteLine("Quel composant modifier ? (x/y/z/all)");
                    string comp = Console.ReadLine().ToLower();
                    if (comp == "all")
                    {
                        Console.Write("X: "); float.TryParse(Console.ReadLine(), out float x);
                        Console.Write("Y: "); float.TryParse(Console.ReadLine(), out float y);
                        Console.Write("Z: "); float.TryParse(Console.ReadLine(), out float z);
                        variable.Data = new Vector3(x, y, z);
                    }
                    else if (comp == "x") { Console.Write("X: "); if (float.TryParse(Console.ReadLine(), out float x)) variable.Data = new Vector3(x, v3.Y, v3.Z); }
                    else if (comp == "y") { Console.Write("Y: "); if (float.TryParse(Console.ReadLine(), out float y)) variable.Data = new Vector3(v3.X, y, v3.Z); }
                    else if (comp == "z") { Console.Write("Z: "); if (float.TryParse(Console.ReadLine(), out float z)) variable.Data = new Vector3(v3.X, v3.Y, z); }
                    break;

                case float[] mat when mat.Length == 16:
                    Console.WriteLine("Modification Matrice 4x4. Index (0-15) :");
                    if (int.TryParse(Console.ReadLine(), out int idx) && idx >= 0 && idx < 16)
                    {
                        Console.Write($"Valeur pour [{idx}] : ");
                        if (float.TryParse(Console.ReadLine(), out float val)) mat[idx] = val;
                    }
                    break;

                default:
                    Console.WriteLine("Type non supporté pour l'édition rapide.");
                    break;
            }
        }

        private void ReadFunction(FileReader reader, Function func, long codePos, long strPos, long dataPos, uint dataSize)
        {
            dynamic GetString(uint offset)
            {
                if (reader.BaseStream.Length <= strPos + offset)
                    return "";

                using (reader.TemporarySeek(strPos + offset, SeekOrigin.Begin))
                {
                    return reader.ReadZeroTerminatedString();
                }
            }

            dynamic GetValue(uint id)
            {
                using (reader.TemporarySeek(dataPos + id * 4, SeekOrigin.Begin))
                {
                    uint value = reader.ReadUInt32();
                    if (Hashing.HashStrings.ContainsKey(value))
                        return value;
                    else
                    {
                        reader.Seek(-4);
                        float v = reader.ReadSingle();
                        if (v > 1000 || v < -10000)
                            return value;
                        return v;
                    }
                }
            }

            reader.SeekBegin(codePos + func.CodeStartIndex * 2);

            List<dynamic> values = new List<dynamic>();
            uint varID = 0;

            uint dataPointer = 0;
            while (true)
            {
                ushort code = reader.ReadUInt16();

                uint val = code & 0xffff03ff;
                uint opCode = (uint)(code >> 10);

                var operation = new Operation()
                {
                    Code = code,
                    OpCode = (OperationCode)opCode,
                    RegValue = val,
                };
                func.Operations.Add(operation);

                switch (opCode)
                {
                    case (uint)OperationCode.READ: //read data
                        //Todo unsure why but sometimes value too big, skip if too big
                        if ((dataSize / 4) > val)
                        {
                            operation.DataRead = GetValue(val);
                            values.Add(operation.DataRead);
                        }
                        break;
                    case (uint)OperationCode.READ_U24: //read data
                        {
                            operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                            val = operation.RegValueEx + val * 0x10000;
                            if ((dataSize / 4) > val)
                            {
                                operation.DataRead = GetValue(val);
                                values.Add(operation.DataRead);
                            }
                            else
                                reader.Seek(-2);
                        }
                        break;
                    case (uint)OperationCode.STRING_OFFSET: //read data
                        operation.String = GetString(val);
                        values.Add(operation.String);
                        break;
                    case (uint)OperationCode.STRING_OFFSETU24: //same as 2 but uint24
                        operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                        val = operation.RegValueEx + val * 0x10000;
                        operation.String = GetString(val);
                        values.Add(val);
                        break;
                    case (uint)OperationCode.SET:  //set value
                        values.Add(val);
                        break;
                    case (uint)5: //Jump?? Adjusts operation reader pos
                        var prev = values.LastOrDefault();
                        operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                        break;
                    case (uint)6: //Same as 5 but no data adjust
                        operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                        break;
                    case (uint)OperationCode.MOV_8:  //mov regions of data to the pointer (0x15)
                        if (values.Count == 0)
                            break;

                        var prevV = values.LastOrDefault();
                        //4 = float/hash
                        //2 = short
                        //1 = byte
                        //8 or higher, buffer
                        switch (val)
                        {
                            case 4: func.AddVariableCmd(dataPointer, prevV); break;
                            case 2: func.AddVariableCmd(dataPointer, (short)prevV); break;
                            case 1: func.AddVariableCmd(dataPointer, (byte)prevV); break;
                            default:
                                throw new Exception();
                        }
                        //Reset list
                        values = new List<dynamic>();
                        break;
                    case (uint)OperationCode.CMD: //This seems to execute different commands. We only use the data type ones atm
                        //821 = vec4
                        //824 = vec3
                        //560 = matrix4x4
                        //434 = make list item with data
                        //57 = boolean?
                        //21 = add to list
                        if (Hashing.GetString(HashType) == "COGScriptInterpreter")
                        {
                            switch ((CommandRun)val)
                            {
                                case CommandRun.MATRIX4X4:
                                    if (values.Count == 16)
                                    {
                                        func.AddVariableCmd(dataPointer, new float[16]
                                        {
                                    values[0],values[1],values[2],values[3],
                                    values[4],values[5],values[6],values[7],
                                    values[8],values[9],values[10],values[11],
                                    values[12],values[13],values[14],values[15],
                                         });
                                    }
                                 else
                                        func.AddUnkCmd(dataPointer, values, val);
                                    break;
                                case CommandRun.VEC3:
                                    if (values.Count == 3)
                                        func.AddVariableCmd(dataPointer, new Vector3(values[0], values[1], values[2]));
                                    else
                                        func.AddUnkCmd(dataPointer, values, val);
                                    break;
                                case CommandRun.VEC4:
                                    if (values.Count == 4)
                                        func.AddVariableCmd(dataPointer, new Vector4(values[0], values[1], values[2], values[3]));
                                    else
                                        func.AddUnkCmd(dataPointer, values, val);
                                    break;
                                case CommandRun.BOOL:
                                    func.AddVariableCmd(dataPointer, values[0] == 1 ? true : false);
                                    break;
                                case CommandRun.LIST_ITEM:
                                    //List<dynamic> list = func.GetList(dataPointer);
                                    // list.AddRange(values);
                                    break;
                                default:
                                    func.AddUnkCmd(dataPointer, values, val);
                                    break;
                            }
                        }
                        else
                        {
                            func.AddUnkCmd(dataPointer, values, val);
                        }
                        //Reset list
                        values = new List<dynamic>();
                        break;
                    case (uint)0x8:
                        {
                            operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                        }
                        break;
                    case (uint)OperationCode.NOOP: // no op
                        break;
                    case (uint)OperationCode.END:
                        //do() or end code
                        //Todo there is code here
                        //Code ends if requirements are met
                        return;
                    case (uint)OperationCode.SHIFT_PTR:
                        dataPointer += val;
                        break;
                    case (uint)OperationCode.PTR:
                        dataPointer = val * 4 + 4;
                        break;
                    case (uint)0x16:
                        {
                            operation.RegValueEx = reader.ReadUInt16(); //reads ahead
                        }
                        break;
                    case (uint)0x1D: //Moves last value to iVar10 + 0xC
                        {
                            //Todo figure out iVar10 + 0xC
                            operation.RegValueEx = reader.ReadUInt16(); //offset from iVar10 + 0xC
                            var v = values.LastOrDefault();

                            //4 = float/hash
                            //2 = short
                            //1 = byte
                            //8 or higher, buffer
                            switch (val)
                            {

                            }
                        }
                        break;
                    default:
                        func.AddCmd(opCode, val);
                        break;
                }
            }
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

        public enum RunCode
        {
            LAB_0077c5d8,
            LAB_0077c774,
            LAB_0077c604,
            LAB_0077c6c8,
            LAB_0077c930,
            LAB_0077cb2c,
            LAB_0077c95c,
            LAB_0077ca34,
            LAB_0077c7a4,
            LAB_0077c9e4,
            LAB_0077c7d0,
            LAB_0077cc18,
            LAB_0077ccd4,
            LAB_0077cc44,
            LAB_0077ca94,
            LAB_0077cbcc,
            LAB_0077cac0,
            LAB_0077cd04,
            LAB_0077cd98,
            LAB_0077cd30,
            LAB_0077c410,
            LAB_0077c514,
            LAB_0077c868,
            LAB_0077ca74,
            LAB_0077c830,
            LAB_0077ca14,
            FUN_0077c5b0,
            LAB_0077c754,
            LAB_0077c3cc,
            LAB_0077c434,
            LAB_0077c534,
            LAB_0077c458,
            LAB_0077c560,
            LAB_0077c58c,
            LAB_0077c6a4,
            LAB_0077c3f4,
            LAB_0077c690,
            LAB_0077c854,
            LAB_0077c88c,
            LAB_0077c47c,
            nlStringHash, //FUN_0077c708
            nlStringLowerHash, //FUN_0077cbac
            LAB_0077cbfc,
            LAB_0077cb5c,
            LAB_0077ccb0,
            LAB_0077c728,
            FUN_0077cb84,
        }

        public enum OperationCode
        {
            //Reads a data value by multiplying the reg value * 4
            READ = 0,
            READ_U24 = 1, //Uses uint16 and combines with reg value to get uint24
            //Uses strings from string table
            STRING_OFFSET = 2, 
            STRING_OFFSETU24 = 3, //Uses uint16 and combines with reg value to get uint24
            //Sets a raw command
            SET =  4,
            //Some sort of command for changing operation reader position
            //6 is similar
            JUMP1 = 5,
            JUMP2 = 6,
            //Sets a command for various purposes
            CMD = 7,
            CMD_U24 = 8, //Reads uint16 and combines with reg value to get uint24

            // 9/10 is releated to hash table

            //Not used
            NOOP = 0xA,
            //If reg value = 43, Executes a function with the last set hash function
            RUN = 0xB,
            //Indexes a hash from the hash table to run a function from 0xB
            SET_FUNC_HASH = 0x14,
            //Ends function 
            END = 0xC,
            //Sets pointer
            PTR = 0xE,
            //Moves data 8 bytes back
            MOV_8 = 0x10,
            //Moves data 4 bytes back
            MOV_4 = 0x11, 
            //Shifts current data pointer for setting data to a struct
            SHIFT_PTR = 0x15,
            SHIFT_PTR_U24 = 0x16, //Reads uint16 and combines with reg value to get uint24
            MAX = 0x27,
        }
    }
}
