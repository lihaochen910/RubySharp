namespace RubySharp.Core.Compiler
{
    using System;
    
    public class Token {
        
        private string     value;
        private TokenType  type;
        public  SourceSpan span;

        public Token ( TokenType type, string value ) {
            this.type  = type;
            this.value = value;
        }

        public Token ( TokenType type, string value, SourceSpan span ) {
            this.type  = type;
            this.value = value;
            this.span  = span;
        }

        public string Value {
            get { return this.value; }
        }

        public TokenType Type {
            get { return this.type; }
        }

        public SourceSpan Span {
            get { return this.span; }
        }

        public override string ToString () {
            return $"{type}: {value} ({span.Start.Line},{span.Start.Column})";
        }
    }
}
