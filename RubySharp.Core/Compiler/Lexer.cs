using RubySharp.Core.Language;


namespace RubySharp.Core.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using RubySharp.Core.Exceptions;


    public class Lexer {
        
        private const char Quote        = '\'';
        private const char DoubleQuote  = '"';
        private const char Colon        = ':';
        private const char StartComment = '#';
        private const char EndOfLine    = '\n';
        private const char Variable     = '@';
        private const char Global       = '$';

        private const string Separators = ";()[],.|{}";

        private static string[] operators = new []
            { "+", "-", "*", "/", "=", "<", ">", "<<", "!", "==", "===", "<=", ">=", "!=", "=>", ":", "->", "..", "..." };

        private ICharStream    stream;
        private Stack< Token > tokens = new Stack< Token > ();
        private SourcePosition tokenStartPosition;
        private SourcePosition tokenEndPosition;

        public Lexer ( string text ) {
            this.stream = new TextCharStream ( text );
        }

        public Lexer ( TextReader reader ) {
            this.stream = new TextReaderCharStream ( reader );
        }

        public Token NextToken () {
            
            if ( this.tokens.Count > 0 )
                return this.tokens.Pop ();

            tokenStartPosition = LexerPosition;

            int ich = this.NextFirstChar ();

            if ( ich == -1 )
                return null;

            char ch = ( char )ich;

            if ( ch == EndOfLine ) {
                tokenEndPosition = LexerPosition;
                return new Token ( TokenType.EndOfLine, "\n", new SourceSpan ( this.tokenStartPosition, this.tokenEndPosition ) );
            }

            if ( ch == Quote )
                return this.NextString ( Quote );

            if ( ch == DoubleQuote )
                return this.NextString ( DoubleQuote );

            if ( ch == Colon )
                return this.NextSymbol ();

            if ( ch == Variable )
                return this.NextInstanceVariableName ();
            
            if ( ch == Global )
                return this.NextGlobalVariableName ();

            if ( operators.Contains ( ch.ToString () ) ) {
                string value = ch.ToString ();
                ich = this.NextChar ();

                if ( ich >= 0 ) {
                    
                    value += ( char )ich;
                    if ( operators.Contains ( value ) ) {
                        tokenEndPosition = LexerPosition;
                        return new Token ( TokenType.Operator, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
                    }

                    this.BackChar ();
                }

                tokenEndPosition = LexerPosition;
                return new Token ( TokenType.Operator, ch.ToString (), new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
            }
            else if ( operators.Any ( op => op.StartsWith ( ch.ToString () ) ) ) {
                string value = ch.ToString ();
                ich = this.NextChar ();

                if ( ich >= 0 ) {
                    value += ( char )ich;
                    if ( operators.Contains ( value ) ) {
                        tokenEndPosition = LexerPosition;
                        return new Token ( TokenType.Operator, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
                    }

                    this.BackChar ();
                }
            }

            if ( Separators.Contains ( ch ) ) {
                tokenEndPosition = LexerPosition;
                return new Token ( TokenType.Separator, ch.ToString (), new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
            }

            if ( char.IsDigit ( ch ) )
                return this.NextInteger ( ch );

            if ( char.IsLetter ( ch ) ||
                 ch == '_' ||
                 ( char )PeekChar () == '&' && ( char.IsLetter ( ch ) || ch == '_' ) ) {
                return NextIdentifier ( ch );
            }

            throw new SyntaxError ( string.Format ( "unexpected '{0}'", ch ), LexerPosition );
        }

        public void PushToken ( Token token ) {
            this.tokens.Push ( token );
        }

        public SourcePosition LexerPosition {
            get {
                int nLine         = 0;
                int nLastLineChar = 0;
                for ( int i = 0; i < stream.Position (); ++i ) {
                    if ( stream.Data ()[ i ].Equals ( '\n' ) ) {
                        nLine++;
                        nLastLineChar = i;
                    }
                }

                int nCol = stream.Position () - nLastLineChar;
                return new SourcePosition ( nLine + 1, nCol, stream.Position () );
            }
        }

        public string SourceData {
            get { return this.stream.Data (); }
        }

        private Token NextIdentifier ( char ch ) {
            string value = ch.ToString ();
            int    ich;

            for ( ich = this.NextChar (); ich >= 0 && ( ( char )ich == '_' || char.IsLetterOrDigit ( ( char )ich ) ); ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 )
                this.BackChar ();

            tokenEndPosition = LexerPosition;

            if ( Predicates.IsConstantName ( value ) ) {
                return new Token ( TokenType.ConstantVarName, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
            }

            return new Token ( TokenType.Identifier, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextInstanceVariableName () {
            string value = string.Empty;
            int    ich;

            for ( ich = this.NextChar ();
                ich >= 0 && ( ( char )ich == '_' || char.IsLetterOrDigit ( ( char )ich ) );
                ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 ) {
                if ( string.IsNullOrEmpty ( value ) && ( char )ich == Variable )
                    return this.NextClassVariableName ();

                this.BackChar ();
            }

            if ( string.IsNullOrEmpty ( value ) || char.IsDigit ( value[ 0 ] ) )
                throw new SyntaxError ( "invalid instance variable name", LexerPosition );

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.InstanceVarName, value,
                new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextClassVariableName () {
            string value = string.Empty;
            int    ich;

            for ( ich = this.NextChar ();
                ich >= 0 && ( ( char )ich == '_' || char.IsLetterOrDigit ( ( char )ich ) );
                ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 )
                this.BackChar ();

            if ( string.IsNullOrEmpty ( value ) || char.IsDigit ( value[ 0 ] ) )
                throw new SyntaxError ( "invalid class variable name", LexerPosition );

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.ClassVarName, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }
        
        private Token NextGlobalVariableName () {
            string value = string.Empty;
            int    ich;

            for ( ich = this.NextChar (); ich >= 0 && ( ( char )ich == '_' || char.IsLetterOrDigit ( ( char )ich ) ); ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 ) {
                this.BackChar ();
            }

            if ( string.IsNullOrEmpty ( value ) || char.IsDigit ( value[ 0 ] ) )
                throw new SyntaxError ( "invalid global variable name", LexerPosition );

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.GlobalVarName, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextSymbol () {
            string value = string.Empty;
            int    ich;

            for ( ich = this.NextChar ();
                ich >= 0 && ( ( char )ich == '_' || char.IsLetterOrDigit ( ( char )ich ) );
                ich = this.NextChar () ) {
                char ch = ( char )ich;

                if ( char.IsDigit ( ch ) && string.IsNullOrEmpty ( value ) )
                    throw new SyntaxError ( "unexpected integer", LexerPosition );

                value += ch;
            }

            if ( ich >= 0 ) {
                char ch = ( char )ich;

                if ( ch == ':' && string.IsNullOrEmpty ( value ) ) {
                    tokenEndPosition = LexerPosition;
                    return new Token ( TokenType.Separator, "::",
                        new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
                }

                this.BackChar ();
            }

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.Symbol, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextString ( char init ) {
            string value = string.Empty;
            int    ich;

            for ( ich = this.NextChar (); ich >= 0 && ( ( char )ich ) != init; ich = this.NextChar () ) {
                char ch = ( char )ich;

                if ( ch == '\\' ) {
                    int ich2 = this.NextChar ();

                    if ( ich2 > 0 ) {
                        char ch2 = ( char )ich2;

                        if ( ch2 == 't' ) {
                            value += '\t';
                            continue;
                        }

                        if ( ch2 == 'r' ) {
                            value += '\r';
                            continue;
                        }

                        if ( ch2 == 'n' ) {
                            value += '\n';
                            continue;
                        }

                        value += ch2;
                        continue;
                    }
                }

                value += ( char )ich;
            }

            if ( ich < 0 )
                throw new SyntaxError ( "unclosed string", LexerPosition );

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.String, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextInteger ( char ch ) {
            string value = ch.ToString ();
            int    ich;

            for ( ich = this.NextChar (); ich >= 0 && char.IsDigit ( ( char )ich ); ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 && ( char )ich == '.' )
                return this.NextFloat ( value );

            if ( ich >= 0 )
                this.BackChar ();

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.Integer, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private Token NextFloat ( string ivalue ) {
            string value = ivalue + ".";
            int    ich;

            for ( ich = this.NextChar (); ich >= 0 && char.IsDigit ( ( char )ich ); ich = this.NextChar () )
                value += ( char )ich;

            if ( ich >= 0 )
                this.BackChar ();

            if ( value.EndsWith ( "." ) ) {
                this.BackChar ();
                tokenEndPosition = LexerPosition;
                return new Token ( TokenType.Integer, ivalue, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
            }

            tokenEndPosition = LexerPosition;
            return new Token ( TokenType.Float, value, new SourceSpan ( tokenStartPosition, tokenEndPosition ) );
        }

        private int NextFirstChar () {
            
            int ich = this.NextChar ();

            while ( true ) {
                while ( ich > 0 && ( char )ich != '\n' && char.IsWhiteSpace ( ( char )ich ) )
                    ich = this.NextChar ();

                if ( ich > 0 && ( char )ich == StartComment ) {
                    for ( ich = this.stream.NextChar (); ich >= 0 && ( char )ich != '\n'; )
                        ich = this.stream.NextChar ();

                    if ( ich < 0 )
                        return -1;

                    continue;
                }

                break;
            }

            return ich;
        }

        private int NextChar () {
            return this.stream.NextChar ();
        }

        private void BackChar () {
            this.stream.BackChar ();
        }

        private int PeekChar ( int i = 1 ) {
            return this.stream.PeekChar ( i );
        }
    }
}
