using System;
using Irony.Parsing;

namespace PL0_Language.Gramatica
{
    public class Pl0Grammar : Grammar
    {
        public Pl0Grammar()
        {
            var number = TerminalFactory.CreateCSharpNumber("number");
            var identifier = TerminalFactory.CreateCSharpIdentifier("identifier");
            var semicolon = ToTerm(";");
            var period = ToTerm(".");
            var comma = ToTerm(",");
            var assign = ToTerm(":=");
            var equal = ToTerm("=");
            var plus = ToTerm("+");
            var minus = ToTerm("-");
            var mult = ToTerm("*");
            var div = ToTerm("/");
            var lparen = ToTerm("(");
            var rparen = ToTerm(")");
            var kwConst = ToTerm("const");
            var kwVar = ToTerm("var");
            var kwProcedure = ToTerm("procedure");
            var kwCall = ToTerm("call");
            var kwBegin = ToTerm("begin");
            var kwEnd = ToTerm("end");
            var kwIf = ToTerm("if");
            var kwThen = ToTerm("then");
            var kwWhile = ToTerm("while");
            var kwDo = ToTerm("do");
            var kwWrite = ToTerm("!");
            var kwRead = ToTerm("?");

            var Program = new NonTerminal("Program");
            var Block = new NonTerminal("Block");
            var ConstDeclOpt = new NonTerminal("ConstDeclOpt");
            var VarDeclOpt = new NonTerminal("VarDeclOpt");
            var ProcDeclList = new NonTerminal("ProcDeclList");
            var ConstDecl = new NonTerminal("ConstDecl");
            var VarDecl = new NonTerminal("VarDecl");
            var ProcDecl = new NonTerminal("ProcDecl");
            var Statement = new NonTerminal("Statement");
            var StatementList = new NonTerminal("StatementList");
            var Expression = new NonTerminal("Expression");
            var Term = new NonTerminal("Term");
            var Factor = new NonTerminal("Factor");
            var Condition = new NonTerminal("Condition");

            Program.Rule = Block + period;
            ConstDecl.Rule = kwConst + identifier + equal + number + semicolon;
            VarDecl.Rule = kwVar + identifier + semicolon;
            ProcDecl.Rule = kwProcedure + identifier + semicolon + Block + semicolon;

            ConstDeclOpt.Rule = Empty | ConstDecl;
            VarDeclOpt.Rule = Empty | VarDecl;
            ProcDeclList.Rule = Empty | MakePlusRule(ProcDeclList, ProcDecl);

            Block.Rule = ConstDeclOpt + VarDeclOpt + ProcDeclList + Statement;

            Statement.Rule = identifier + assign + Expression
                            | kwCall + identifier
                            | kwBegin + StatementList + kwEnd
                            | kwIf + Condition + kwThen + Statement
                            | kwWhile + Condition + kwDo + Statement
                            | kwWrite + Expression
                            | kwRead + identifier
                            | Empty;

            StatementList.Rule = MakePlusRule(StatementList, semicolon, Statement);

            Expression.Rule = Expression + plus + Term
                            | Expression + minus + Term
                            | Term;

            Term.Rule = Term + mult + Factor
                      | Term + div + Factor
                      | Factor;

            Factor.Rule = identifier | number | lparen + Expression + rparen;

            Condition.Rule = Expression + equal + Expression;

            Root = Program;

            MarkReservedWords("const", "var", "procedure", "call", "begin", "end", "if", "then", "while", "do");
            MarkPunctuation(";", ",", ":=", "=", ".", "(", ")");
        }
    }
}
