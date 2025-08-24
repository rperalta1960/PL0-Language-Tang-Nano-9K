using System;
using Irony.Parsing;
using PL0_Language.Gramatica;
using PL0_Language.Codegen;

namespace PL0_Language
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string code = @"
                const a = 10;
                var x;
                procedure suma;
                begin
                    x := x + x
                end;
                begin
                    x := a;
                    call suma;
                    !x
                end.
            ";

            var grammar = new Pl0Grammar();
            var parser = new Parser(new LanguageData(grammar));
            var tree = parser.Parse(code);

            if (tree.HasErrors())
            {
                Console.WriteLine("Errores de compilación encontrados:");
                foreach (var msg in tree.ParserMessages)
                {
                    Console.WriteLine($"[Línea {msg.Location.Line + 1}, Columna {msg.Location.Column + 1}] {msg.Message}");
                }
                return;
            }

            Console.WriteLine("Árbol de análisis sintáctico:");
            PrintTree(tree.Root, 0);

            // Generar ensamblador J1
            var gen = new CodeGenerator();
            var asm = gen.Generate(tree.Root);

            Console.WriteLine("\n=== Ensamblador J1 (optimizado) ===\n");
            Console.WriteLine(asm);

            File.WriteAllText("out.j1.s", asm);
            Console.WriteLine("\nGuardado en out.j1.s");

            // << Montar .s -> .hex y .lst
            var assembler = new AssemblerJ1();
            var result = assembler.Assemble(asm);

            File.WriteAllLines("out.j1.hex", result.HexLines); // cada línea: palabra de 16 bits en HEX (XXXX)
            File.WriteAllText("out.j1.lst", result.Listing);   // listado con dirección, hex y fuente

            Console.WriteLine("Guardado en out.j1.hex y out.j1.lst");
        }

        static void PrintTree(ParseTreeNode node, int indent = 0)
        {
            string indentStr = new string(' ', indent * 2);
            if (node.Token != null)
                Console.WriteLine($"{indentStr}{node.Term.Name} -> '{node.Token.Text}'");
            else
                Console.WriteLine($"{indentStr}{node.Term.Name}");

            foreach (var child in node.ChildNodes)
                PrintTree(child, indent + 1);
        }
    }
}
