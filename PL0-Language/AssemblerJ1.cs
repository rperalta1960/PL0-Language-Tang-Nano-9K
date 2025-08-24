﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PL0_Language.Codegen
{
    public sealed class AssemblerResult
    {
        public IReadOnlyList<string> HexLines { get; }
        public string Listing { get; }
        public AssemblerResult(IReadOnlyList<string> hexLines, string listing)
        {
            HexLines = hexLines; Listing = listing;
        }
    }

    public sealed class AssemblerJ1
    {
        private static readonly Dictionary<string, ushort> ALU = new(StringComparer.OrdinalIgnoreCase)
        {
            { "DUP", 0x6081 }, { "OVER", 0x6181 }, { "INVERT", 0x6600 },
            { "ADD", 0x6203 }, { "SUB", 0x6303 }, { "SWAP", 0x6180 },
            { "NIP", 0x6003 }, { "DROP", 0x6103 }, { "EXIT", 0x7018 },
            { "@",   0x6C00 }, { "!",    0x6123 }, { ">R",   0x61CB },
            { "R>",  0x7B99 }, { "R@",   0x7B81 },
        };

        private sealed record Insn(int LineNo, string Source, string Mn, string? Arg, int Address);

        public AssemblerResult Assemble(string asm)
        {
            var lines = SplitLines(asm);
            var labels = new Dictionary<string, int>(StringComparer.Ordinal);
            var pass1 = new List<Insn>(lines.Count);

            int pc = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var raw = lines[i];
                var (code, _) = StripComment(raw);
                if (string.IsNullOrWhiteSpace(code)) continue;

                var colon = code.IndexOf(':');
                if (colon >= 0)
                {
                    var label = code[..colon].Trim();
                    if (!IsIdentifier(label)) throw new Exception($"Línea {i + 1}: etiqueta inválida '{label}'.");
                    if (labels.ContainsKey(label)) throw new Exception($"Línea {i + 1}: etiqueta duplicada '{label}'.");
                    labels[label] = pc;
                    code = code[(colon + 1)..].Trim();
                    if (string.IsNullOrWhiteSpace(code)) continue;
                }

                var (mn, arg) = SplitMnemonic(code);
                if (IsInstruction(mn)) { pass1.Add(new Insn(i + 1, raw, mn, arg, pc)); pc++; }
                else throw new Exception($"Línea {i + 1}: mnemónico desconocido '{mn}'.");
            }

            var hex = new List<string>(pass1.Count);
            var lst = new StringBuilder();
            foreach (var ins in pass1)
            {
                ushort word = Encode(ins, labels);
                hex.Add(word.ToString("X4"));
                lst.AppendLine($"{ins.Address:X4}  {word:X4}    {ins.Source}");
            }

            return new AssemblerResult(hex, lst.ToString());
        }

        private static bool IsInstruction(string mn)
        {
            mn = mn.Trim();
            if (mn.Equals("LIT", StringComparison.OrdinalIgnoreCase)) return true;
            if (mn.Equals("JUMP", StringComparison.OrdinalIgnoreCase)) return true;
            if (mn.Equals("0BRANCH", StringComparison.OrdinalIgnoreCase)) return true;
            if (mn.Equals("CALL", StringComparison.OrdinalIgnoreCase)) return true;
            if (ALU.ContainsKey(mn)) return true;
            return false;
        }

        private static (string mn, string? arg) SplitMnemonic(string text)
        {
            var sp = text.Trim().Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
            if (sp.Length == 0) return ("", null);
            if (sp.Length == 1) return (sp[0], null);
            return (sp[0], sp[1].Trim());
        }

        private static (string code, string? comment) StripComment(string line)
        {
            if (line is null) return ("", null);
            int p = line.IndexOf(';');
            if (p < 0) return (line.Trim(), null);
            return (line[..p].Trim(), line[p..]);
        }

        private static bool IsIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!(char.IsLetter(s[0]) || s[0] == '_')) return false;
            for (int i = 1; i < s.Length; i++)
                if (!(char.IsLetterOrDigit(s[i]) || s[i] == '_')) return false;
            return true;
        }

        private static bool TryParseInt(string tok, out int value)
        {
            tok = tok.Trim();
            if (tok.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(tok[2..], NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out value);
            return int.TryParse(tok, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        private static ushort Encode(Insn ins, Dictionary<string, int> labels)
        {
            string mn = ins.Mn.ToUpperInvariant();
            string? arg = ins.Arg;

            if (mn == "LIT")
            {
                int val = ResolveArgToInt(ins, arg, labels);
                if (val < 0 || val > 0x7FFF) throw new Exception($"Línea {ins.LineNo}: LIT fuera de rango (0..32767).");
                return (ushort)(0x8000 | (val & 0x7FFF));
            }
            if (mn == "JUMP") return (ushort)(0x0000 | (ResolveArgToInt(ins, arg, labels) & 0x1FFF));
            if (mn == "0BRANCH") return (ushort)(0x2000 | (ResolveArgToInt(ins, arg, labels) & 0x1FFF));
            if (mn == "CALL") return (ushort)(0x4000 | (ResolveArgToInt(ins, arg, labels) & 0x1FFF));

            if (ALU.TryGetValue(mn, out var w)) return w;
            throw new Exception($"Línea {ins.LineNo}: mnemónico no soportado '{mn}'.");
        }

        private static int ResolveArgToInt(Insn ins, string? arg, Dictionary<string, int> labels)
        {
            if (string.IsNullOrWhiteSpace(arg))
                throw new Exception($"Línea {ins.LineNo}: falta argumento para '{ins.Mn}'.");
            if (TryParseInt(arg, out int n)) return n;
            if (IsIdentifier(arg) && labels.TryGetValue(arg, out int addr)) return addr;
            throw new Exception($"Línea {ins.LineNo}: argumento inválido '{arg}'.");
        }

        private static List<string> SplitLines(string text)
            => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
    }
}

