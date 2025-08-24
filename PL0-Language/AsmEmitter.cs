using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PL0_Language.Codegen
{
    internal sealed class AsmEmitter
    {
        private readonly List<string> _lines = new();
        private int _lab = 0;
        public string NewLabel(string prefix = "L") => $"{prefix}{_lab++}";
        public void Mark(string label) => _lines.Add($"{label}:");
        public void Emit(string text) => _lines.Add(text);
        public void Comment(string text) => _lines.Add($"; {text}");

        public string GetText()
        {
            var lines = Peephole(_lines);
            var sb = new StringBuilder();
            foreach (var ln in lines) sb.AppendLine(ln);
            return sb.ToString();
        }

        // Peephole minimal: elimina pares redundantes y constantes triviales
        private static IEnumerable<string> Peephole(List<string> src)
        {
            var dst = new List<string>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                var cur = src[i];

                // eliminar comentarios múltiples consecutivos
                if (cur.StartsWith(";") && dst.Count > 0 && dst[^1].StartsWith(";")) continue;

                // ejemplo: LIT 0 ; ADD  => no hacer nada (sumar cero), si el patrón es exacto
                if (i + 1 < src.Count && cur.Trim() == "LIT 0" && src[i + 1].Trim() == "ADD")
                { i++; continue; }

                // SWAP seguido de SWAP
                if (i + 1 < src.Count && cur.Trim() == "SWAP" && src[i + 1].Trim() == "SWAP")
                { i++; continue; }

                // DUP seguido de DROP
                if (i + 1 < src.Count && cur.Trim() == "DUP" && src[i + 1].Trim() == "DROP")
                { i++; continue; }

                dst.Add(cur);
            }
            return dst;
        }
    }
}
