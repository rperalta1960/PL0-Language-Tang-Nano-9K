using System;
using System.Collections.Generic;
using System.Linq;

namespace PL0_Language.Codegen
{
    internal enum SymKind { Const, Var, Param, Local }

    internal sealed class Symbol
    {
        public string Name { get; }
        public string Scope { get; }
        public SymKind Kind { get; }
        public int Value { get; }     // válido si Const
        public int Address { get; }   // válido si Var global
        public int Offset { get; }    // válido si Param/Local (offset relativo a FP)

        // Constante
        public Symbol(string scope, string name, int value)
        {
            Scope = scope; Name = name; Kind = SymKind.Const;
            Value = value; Address = -1; Offset = 0;
        }

        // Variable global
        public Symbol(string scope, string name, int address, bool _varMarker_)
        {
            Scope = scope; Name = name; Kind = SymKind.Var;
            Address = address; Value = 0; Offset = 0;
        }

        // Parámetro o Local
        public Symbol(string scope, string name, SymKind kind, int offset)
        {
            Scope = scope; Name = name; Kind = kind;
            Offset = offset; Address = -1; Value = 0;
        }

        public string Key => $"{Scope}::{Name}";
    }

    internal sealed class SymbolTable
    {
        private readonly Dictionary<string, Symbol> _map = new(StringComparer.Ordinal);
        private readonly Stack<string> _scopes = new();
        private int _nextGlobalAddr;

        private sealed class FrameLayout
        {
            public readonly List<string> Params = new();
            public readonly List<string> Locals = new();
        }
        private readonly Dictionary<string, FrameLayout> _layouts = new(StringComparer.Ordinal);

        public SymbolTable(int startGlobalAddr = 0x0100) { _nextGlobalAddr = startGlobalAddr; }

        public string CurrentScope => _scopes.Count == 0 ? "" : _scopes.Peek();
        public void EnterScope(string name) => _scopes.Push(name);
        public void ExitScope() { if (_scopes.Count > 0) _scopes.Pop(); }

        private string Key(string name, string? scope = null) => $"{(scope ?? CurrentScope)}::{name}";
        private string GlobalKey(string name) => $"::{name}";

        // ===== Globales =====
        public void AddConst(string name, int value, string? scope = null)
        {
            string sc = scope ?? CurrentScope;
            var key = Key(name, sc);
            if (_map.TryGetValue(key, out var prev) && prev.Kind != SymKind.Const)
                throw new InvalidOperationException($"'{name}' ya existe como no-const en ámbito '{sc}'.");
            _map[key] = new Symbol(sc, name, value);
        }

        public void AddGlobalVar(string name)
        {
            string sc = ""; // global
            var key = $"{sc}::{name}";
            if (_map.TryGetValue(key, out var prev) && prev.Kind == SymKind.Const)
                throw new InvalidOperationException($"'{name}' ya es const global.");
            if (!_map.ContainsKey(key))
                _map[key] = new Symbol(sc, name, _nextGlobalAddr++, _varMarker_: true);
        }

        public int GetGlobalVarAddr(string name)
        {
            if (_map.TryGetValue(GlobalKey(name), out var g) && g.Kind == SymKind.Var) return g.Address;
            throw new InvalidOperationException($"'{name}' no es var global declarada.");
        }

        public int? GetConst(string name)
        {
            if (_map.TryGetValue(Key(name), out var s) && s.Kind == SymKind.Const) return s.Value;
            if (_map.TryGetValue(GlobalKey(name), out var g) && g.Kind == SymKind.Const) return g.Value;
            return null;
        }

        // ===== Subprogramas (frames) =====
        public void BeginSubprogram(string name)
        {
            EnterScope(name);
            if (!_layouts.ContainsKey(name))
                _layouts[name] = new FrameLayout();
        }
        public void EndSubprogram() => ExitScope();

        public void AddParam(string name)
        {
            var fn = CurrentScope;
            var lay = _layouts[fn];
            if (!lay.Params.Contains(name)) lay.Params.Add(name);
            int ofs = lay.Params.Count; // p1=+1, p2=+2...
            _map[Key(name)] = new Symbol(fn, name, SymKind.Param, ofs);
        }

        public void AddLocal(string name)
        {
            var fn = CurrentScope;
            var lay = _layouts[fn];
            if (!lay.Locals.Contains(name)) lay.Locals.Add(name);
            int ofs = lay.Params.Count + lay.Locals.Count; // tras los params
            _map[Key(name)] = new Symbol(fn, name, SymKind.Local, ofs);
        }

        public void AddLocals(IEnumerable<string> names)
        {
            foreach (var n in names) AddLocal(n);
        }

        public bool TryGetOffset(string name, out int offset)
        {
            if (_map.TryGetValue(Key(name), out var s) && (s.Kind == SymKind.Param || s.Kind == SymKind.Local))
            { offset = s.Offset; return true; }
            offset = 0; return false;
        }

        public (int paramCount, int localCount) FrameSizes(string subr)
        {
            if (_layouts.TryGetValue(subr, out var l)) return (l.Params.Count, l.Locals.Count);
            return (0, 0);
        }

        // Debug opcional
        public string DebugDump()
        {
            var lines = new List<string>();
            foreach (var kv in _map.OrderBy(k => k.Key))
                lines.Add($"{kv.Key} -> {kv.Value.Kind} addr={kv.Value.Address} ofs={kv.Value.Offset} val={kv.Value.Value}");
            return string.Join(Environment.NewLine, lines);
        }
    }
}
