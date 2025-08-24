// Symbols.cs
using System;
using System.Collections.Generic;

namespace PL0_Language.Codegen
{
    internal sealed class Symbol
    {
        public string Name { get; }
        public bool IsConst { get; }
        public int Value { get; }      // válido si es const
        public int Address { get; }    // válido si es var

        // Constructor para constantes
        public Symbol(string name, int value)
        {
            Name = name;
            IsConst = true;
            Value = value;
            Address = -1;
        }

        // Constructor para variables
        public Symbol(string name, int address, bool isVar)
        {
            Name = name;
            IsConst = false;
            Address = address;
            Value = 0;
        }
    }

    internal sealed class SymbolTable
    {
        private readonly Dictionary<string, Symbol> _map = new(StringComparer.Ordinal);
        private int _nextAddr;

        public SymbolTable(int startAddr = 0x0100) { _nextAddr = startAddr; }

        public bool Contains(string name) => _map.ContainsKey(name);

        public void AddConst(string name, int value)
        {
            if (_map.TryGetValue(name, out var prev) && !prev.IsConst)
                throw new InvalidOperationException($"'{name}' ya existe como variable; no se puede redeclarar como const.");
            _map[name] = new Symbol(name, value);
        }

        public void AddVar(string name)
        {
            if (_map.TryGetValue(name, out var prev) && prev.IsConst)
                throw new InvalidOperationException($"'{name}' ya existe como const; no se puede redeclarar como var.");
            if (!_map.ContainsKey(name))
                _map[name] = new Symbol(name, _nextAddr++, isVar: true);
            // si ya existe como var, no hacemos nada (idempotente)
        }

        public void AddVars(IEnumerable<string> names)
        {
            foreach (var n in names) AddVar(n);
        }

        public bool TryGet(string name, out Symbol sym) => _map.TryGetValue(name, out sym);

        public int? GetConst(string name)
            => _map.TryGetValue(name, out var s) && s.IsConst ? s.Value : null;

        public int GetVarAddr(string name)
        {
            if (!_map.TryGetValue(name, out var s))
                throw new InvalidOperationException($"'{name}' no está declarado.");
            if (s.IsConst)
                throw new InvalidOperationException($"'{name}' es una constante; no es variable.");
            return s.Address;
        }

        // (Opcional) ayuda para depuración
        public string DebugDump()
        {
            var lines = new List<string>();
            foreach (var kv in _map)
                lines.Add($"{kv.Key} -> {(kv.Value.IsConst ? "const" : "var")}  addr={kv.Value.Address} val={kv.Value.Value}");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
