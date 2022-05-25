using DbgX;
using DbgX.Interfaces.Services;
using DbgX.Requests;
using DbgX.Requests.Initialization;
using Namotion.Reflection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AsmSearch
{
    internal class DisassemblyCache
    {
        Dictionary<ulong, Instruction> m_instructions = new Dictionary<ulong, Instruction>();

        public async Task DisassembleModule(DebugEngine engine, string modPath)
        {
            // TODO: Use the binary's hash to check if it's a match
            if (File.Exists(modPath + ".discache"))
            {
                var serializer = new JsonSerializer();

                using (StreamWriter sw = new StreamWriter(modPath + ".discache"))
                {
                    using (var jsonWriter = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(jsonWriter, m_instructions);
                    }
                }
            }
            else
            {
                await DisassembleFromBinary(engine, modPath);

                Console.WriteLine($"Finished disassembling {modPath}");
                Console.WriteLine($"Total bytes disassembled: {m_instructions.Values.Sum(x => x.Length)}");

                var serializer = new JsonSerializer();

                using (StreamWriter sw = new StreamWriter(modPath + ".discache"))
                {
                    using (var jsonWriter = new JsonTextWriter(sw))
                    {
                        serializer.Serialize(jsonWriter, m_instructions);
                    }
                }
            }

            while (true)
            {
                string? query = Console.ReadLine();
                if (query == null)
                {
                    break;
                }
                var results = m_instructions.Where(x => x.Value.Text.Contains(query));
                foreach (var result in results)
                {
                    Console.WriteLine(result.Value.Text);
                }
            }
        }

        private async Task DisassembleFromBinary(DebugEngine engine, string modPath)
        {
            await engine.SendRequestAsync(new OpenDumpFileRequest(modPath, new EngineOptions()));
            await engine.SendRequestAsync(new ExecuteRequest(".load DbgModelApiXtn.dll"));
            await engine.SendRequestAsync(new ExecuteRequest(".reload -f"));
            await engine.SendRequestAsync(new ExecuteRequest("lm"));

            // Get exports/symbols from the DLL to use as starting points.
            // Note: Would be nice to do this from the data model but we don't have that in this package right now.
            string allSymbols = await engine.SendRequestAsync(new ExecuteToStringRequest("x *!*"));
            Stack<ulong> addresses = new Stack<ulong>();
            foreach (var addrString in allSymbols.Split('\n').Select(x => x.Split(' ').First()))
            {
                string str;
                if (addrString.Length == 17 && addrString[8] == '`')
                {
                    str = addrString.Substring(0, 8) + addrString.Substring(9, 8);
                }
                else
                {
                    str = addrString;
                }

                ulong addr;
                if (ulong.TryParse(str, System.Globalization.NumberStyles.HexNumber, null, out addr))
                {
                    addresses.Push(addr);
                }
            }

            int totalRoots = 0;

            while (addresses.Any())
            {
                var addr = addresses.Pop();
                await AddInstructionsToCache(engine, addr, addresses);
                totalRoots++;
                if (totalRoots % 100 == 0)
                {
                    Console.WriteLine($"Total roots explored: {totalRoots}, current amount remaining: {addresses.Count()}");
                    Console.WriteLine($"Total bytes disassembled: {m_instructions.Values.Sum(x => x.Length)}");
                }
            }
        }

        private async Task AddInstructionsToCache(DebugEngine engine, ulong addr, Stack<ulong> addressesToVisit)
        {
            if (m_instructions.ContainsKey(addr))
            {
                return;
            }
            var query = $"Debugger.Utility.Code.CreateDisassembler().DisassembleFunction(0x{addr:x})" +
                        ".BasicBlocks.Select(x => new { " + 
                            "Instructions = x.Instructions.Select(y => new {Address = y.Address, Length = y.Length, DisplayString = y.ToDisplayString()})," +
                            "OutBound = x.OutboundControlFlows.Select(y => y.TargetInstruction.Address)" +
                        "})";

            var blocksXml = await engine.SendRequestAsync(new ModelQueryRequest(query, false, DbgX.Interfaces.Enums.ModelQueryFlags.Default, 4));

            DataModelObject blockObjects = new DataModelObject(blocksXml);

            foreach (var block in blockObjects.IteratedChildren)
            {
                var instructions = block["Instructions"];

                bool foundNewInstructions = false;
                foreach (var instruction in instructions.IteratedChildren)
                {
                    var address = instruction["Address"].ValueAsInt;
                    var length = instruction["Length"].ValueAsInt;
                    if (!m_instructions.ContainsKey(address))
                    {
                        m_instructions.Add(address, new Instruction(instruction["DisplayString"].DisplayValue, address, (int)length));
                        foundNewInstructions = true;
                    }
                }

                if (foundNewInstructions)
                {
                    // Find all outbound control flow and use those as starting points. This can find jumps and calls outside of the function.
                    foreach (var controlFlow in block["OutBound"].IteratedChildren)
                    {
                        var address = controlFlow.ValueAsInt;
                        if (!addressesToVisit.Contains(address))
                        {
                            addressesToVisit.Push(address);
                        }
                    }
                }
            }

        }
    }
}
