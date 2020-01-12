namespace RubySharp.Core.Compiler {
    
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using RubySharp.Core.Exceptions;
    using RubySharp.Core.Language;


    public class AstParser {
        
        private const string CLASS    = "class";
        private const string MODULE   = "module";
        private const string DEF      = "def";
        private const string BEGIN    = "begin";
        private const string IF       = "if";
        private const string UNLESS   = "unless";
        private const string WHILE    = "while";
        private const string UNTIL    = "until";
        private const string FOR      = "for";
        private const string UNDEF    = "undef";
        private const string RESCUE   = "rescue";
        private const string ENSURE   = "ensure";
        private const string END      = "end";
        private const string THEN     = "then";
        private const string ELSIF    = "elsif";
        private const string ELSE     = "else";
        private const string CASE     = "case";
        private const string WHEN     = "when";
        private const string BREAK    = "break";
        private const string NEXT     = "next";
        private const string REDO     = "redo";
        private const string RETRY    = "retry";
        private const string IN       = "in";
        private const string DO       = "do";
        private const string RETURN   = "return";
        private const string YIELD    = "yield";
        private const string SUPER    = "super";
        private const string SELF     = "self";
        private const string NIL      = "nil";
        private const string TRUE     = "true";
        private const string FALSE    = "false";
        private const string AND      = "and";
        private const string OR       = "or";
        private const string NOT      = "not";
        private const string ALIAS    = "alias";
        private const string LINE     = "__LINE__";
        private const string FILE     = "__FILE__";
        private const string ENCODING = "__ENCODING__";
        
        private static string[][] binaryoperators = new string[][] {
            new [] { "..", "...", "==", "===", "<=>", "!=", "<", ">", "<=", ">=" },
            new [] { "<<", ">>" },
            new [] { "+", "-" },
            new [] { "*", "/", "%" },
            new [] { "**" }
        };
        
        public string filepath;

        private Lexer lexer;
        private Token t;

        public AstParser ( string text ) {
            lexer = new Lexer ( text );
        }

        public AstParser ( TextReader reader ) {
            lexer = new Lexer ( reader );
        }

        public string ParserPositionStr {
            get {
                SourcePosition pos           = lexer.LexerPosition;
                string         ret           = "";
                int            nLine         = 0;
                int            nLastLineChar = 0;
                for ( int i = 0; i < pos.Index; ++i ) {
                    if ( lexer.SourceData[ i ].Equals ( '\n' ) ) {
                        nLine++;
                        nLastLineChar = i;
                    }
                }

                int nCol = pos.Index - nLastLineChar;
                ret += $"Line: {nLine}, Column: {nCol}\n";

                int nNextLine = pos.Index;
                while ( nNextLine < lexer.SourceData.Length && !lexer.SourceData[ nNextLine ].Equals ( '\n' ) )
                    nNextLine++;

                ret += lexer.SourceData.Substring ( nLastLineChar, nNextLine - nLastLineChar ) + "\n";
                ret += new String ( ' ', nCol );
                ret += "^";
                return ret;
            }
        }

        public AstParserState Parse () {
            AstParserState state = new AstParserState ();
            var tree = new ExpressionListNode ();
            
            for ( var command = ParseCommand (); command != null; command = ParseCommand () ) {
                tree.Push ( command );
            }

            state.filename = filepath;
            state.tree = tree;
            return state;
        }

        public AstNode ParseExpression () {
            
            AstNode expr = ParseNoAssignExpression ();

            if ( expr == null ) {
				return null;
			}

			if ( expr.type != AstNodeType.SYM && expr.type != AstNodeType.LVAR &&
                 expr.type != AstNodeType.CVAR && expr.type != AstNodeType.IVAR &&
                 expr.type != AstNodeType.GVAR && expr.type != AstNodeType.CONST &&
                 expr.type != AstNodeType.DOT && expr.type != AstNodeType.AREF ) {
                return expr;
            }

            t = lexer.NextToken ();

            if ( t == null ) {
				return expr;
			}

            // Asgn Expr
			if ( t.Type == TokenType.Operator && t.Value == "=" ) {
                
                AstNode assignexpr = null;

                switch ( expr.type ) {
                    case AstNodeType.SYM:
                    case AstNodeType.LVAR:
                    case AstNodeType.CVAR:
                    case AstNodeType.IVAR:
                    case AstNodeType.GVAR:
                    case AstNodeType.CONST:
                    case AstNodeType.DOT:
                    case AstNodeType.AREF:
                        assignexpr = new AsgnNode ( expr, ParseExpression () );
                        break;
                }

                return assignexpr;
            }
            
            lexer.PushToken ( t );
            return expr;
        }

        public AstNode ParseCommand () {
            
            t = lexer.NextToken ();

            while ( t != null && IsEndOfCommand ( t ) )
                t = lexer.NextToken ();

            if ( t == null )
                return null;

            lexer.PushToken ( t );

            AstNode expr = ParseExpression ();
            
            // Console.WriteLine ( $"ParseExpression: {expr} {lexer.LexerPosition}" );
            
            ParseEndOfCommand ();
            return expr;
        }

        private AstNode ParseNoAssignExpression () {
            
            var result = ParseBinaryExpression ( 0 );

            if ( !( result is INamedNode ) ) {
                return result;
            }

            var nexpr = result;

            if ( TryParseToken ( TokenType.Separator, "{" ) ) {
                return NODE_LINENO ( new CallNode ( null, nexpr, ParseBlockExpression ( true ) ), t );
            }

            if ( TryParseName ( DO ) ) {
                return NODE_LINENO ( new CallNode ( null, nexpr, ParseBlockExpression () ), t );
            }

            if ( !NextTokenStartsExpressionList () ) {
                return result;
            }

            return NODE_LINENO ( new CallNode ( null, result, ParseExpressionListAsArgs () ), t );
        }

        private IfNode ParseIfExpression () {

            Token token = t;
            AstNode condition = ParseExpression ();
            
            if ( TryParseName ( THEN ) )
                TryParseEndOfLine ();
            else
                ParseEndOfCommand ();

            AstNode thencommand = ParseCommandList ( new [] { END, ELSIF, ELSE } );
            AstNode elsecommand = null;

            if ( TryParseName ( ELSIF ) )
                elsecommand = ParseIfExpression ();
            else if ( TryParseName ( ELSE ) )
                elsecommand = ParseCommandList ();
            else
                ParseName ( END );

            return NODE_LINENO ( new IfNode ( condition, thencommand, elsecommand ), token );
        }
        
        private IfNode ParseUnlessExpression () {

            Token token = t;
            AstNode condition = ParseExpression ();
            if ( TryParseName ( THEN ) )
                TryParseEndOfLine ();
            else
                ParseEndOfCommand ();

            AstNode thencommand = ParseCommandList ( new [] { END, ELSE } );
            AstNode elsecommand = null;
            
            if ( TryParseName ( ELSE ) )
                elsecommand = ParseCommandList ();
            else
                ParseName ( END );

            return NODE_LINENO ( new IfNode ( condition, elsecommand, thencommand ), token );
        }

        private ForNode ParseForInExpression () {

            Token token = t;
            Token nameToken;
            string name = ParseName ( out nameToken );
            ParseName ( IN );
            AstNode expression = ParseExpression ();
            if ( TryParseName ( DO ) )
                TryParseEndOfLine ();
            else
                ParseEndOfCommand ();
            ExpressionListNode command = ParseCommandList ();
            return NODE_LINENO ( new ForNode ( NODE_LINENO ( new SymNode ( name ), nameToken ), expression, command ), token );
        }

        private WhileNode ParseWhileExpression () {
            Token token = t;
            AstNode condition = ParseExpression ();
            if ( TryParseName ( DO ) )
                TryParseEndOfLine ();
            else
                ParseEndOfCommand ();
            ExpressionListNode command = ParseCommandList ();
            return NODE_LINENO ( new WhileNode ( condition, command ), token );
        }

        private UntilNode ParseUntilExpression () {

            Token token = t;
            AstNode condition = ParseExpression ();
            if ( TryParseName ( DO ) )
                TryParseEndOfLine ();
            else
                ParseEndOfCommand ();
            AstNode command = ParseCommandList ();
            return NODE_LINENO ( new UntilNode ( condition, command ), token );
        }

        private DefNode ParseDefExpression () {

            Token token = t;
            AstNode named = ParseFunctionNamePathExpression ();
            ArgsNode parameters = ParseParameterList ();
			ExpressionListNode body = ParseCommandList ();

			return NODE_LINENO ( new DefNode ( named, parameters, body ), token );
        }

        private ClassNode ParseClassExpression () {

            Token token = t;
            AstNode named = ParseNamePathExpression ();
            AstNode super = ParseSuperClassExpression ();

            ParseEndOfCommand ();

            ExpressionListNode body = ParseCommandList ();

            // if ( !Predicates.IsConstantName ( named.Name ) )
            //     throw new SyntaxError ( "class/module name must be a CONSTANT", lexer.LexerPosition );

            return NODE_LINENO ( new ClassNode ( named, super, body ), token );
        }

		private AstNode ParseSuperClassExpression () {

            if ( TryParseToken ( TokenType.Separator, "<" ) ||
                 TryParseToken ( TokenType.Operator, "<" ) ) {
                AstNode named = ParseNamePathExpression ();
				return named;
			}

            return null;
		}

		private ModuleNode ParseModuleExpression () {

			Token token = t;
            AstNode named = ParseNamePathExpression ();
            ExpressionListNode body = ParseCommandList ();

            // if ( !Predicates.IsConstantName ( named.Name ) )
            //     throw new SyntaxError ( "class/module name must be a CONSTANT", lexer.LexerPosition );

            return NODE_LINENO ( new ModuleNode ( named, body ), token );
        }

        private ArgsNode ParseParameterList ( bool canhaveparens = true ) {

            Token token = null;
            ArgsNode parameters = new ArgsNode ();

            bool inparentheses = false;

            if ( canhaveparens )
                inparentheses = TryParseToken ( TokenType.Separator, "(" );

            for ( string name = TryParseName ( out token ); name != null; name = ParseName ( out token ) ) {

                if ( name.StartsWith ( "&" ) ) {
                    parameters.block = NODE_LINENO ( new SymNode ( name ), token );
                }
                else {
                    parameters.Push ( NODE_LINENO ( new SymNode ( name ), token ) );
                }
                
                if ( !TryParseToken ( TokenType.Separator, "," ) )
                    break;
            }

            if ( inparentheses )
                ParseToken ( TokenType.Separator, ")" );

            return parameters;
        }

        private ExpressionListNode ParseExpressionList () {
            ExpressionListNode expressions = new ExpressionListNode ();

            bool inparentheses = TryParseToken ( TokenType.Separator, "(" );

            for ( AstNode expression = ParseExpression ();
                expression != null;
                expression = ParseExpression () ) {
                expressions.Push ( expression );
                if ( !TryParseToken ( TokenType.Separator, "," ) )
                    break;
            }

            if ( inparentheses ) {
                ParseToken ( TokenType.Separator, ")" );
                if ( TryParseName ( "do" ) )
                    expressions.Push ( ParseBlockExpression () );
                else if ( TryParseToken ( TokenType.Separator, "{" ) )
                    expressions.Push ( ParseBlockExpression ( true ) );
            }

            return expressions;
        }
        
        /// <summary>
        /// puts ((1..20).to_a)    (ok)
        /// puts (1..20).to_a      (error)
        /// </summary>
        /// <returns></returns>
        private ArgsNode ParseExpressionListAsArgs () {
            
            ArgsNode parameters = new ArgsNode ();

            bool inparentheses = TryParseToken ( TokenType.Separator, "(" );

            for ( AstNode expression = ParseExpression ();
                expression != null;
                expression = ParseExpression () ) {
                parameters.Push ( expression );
                if ( !TryParseToken ( TokenType.Separator, "," ) )
                    break;
            }

            if ( inparentheses ) {
                
                ParseToken ( TokenType.Separator, ")" );
                
                if ( TryParseName ( DO ) ) {
                    parameters.Push ( ParseBlockExpression () );
                }
                else if ( TryParseToken ( TokenType.Separator, "{" ) ) {
                    parameters.Push ( ParseBlockExpression ( true ) );
                }
            }

            return parameters;
        }

        private IList< AstNode > ParseExpressionList ( string separator ) {
            
            IList<AstNode> expressions = new List<AstNode> ();

            for ( AstNode expression = ParseExpression ();
                expression != null;
                expression = ParseExpression () ) {
                expressions.Add ( expression );
                if ( !TryParseToken ( TokenType.Separator, "," ) )
                    break;
            }

            ParseToken ( TokenType.Separator, separator );

            return expressions;
        }

        private BlockNode ParseBlockExpression ( bool usebraces = false ) {
            
            if ( TryParseToken ( TokenType.Separator, "|" ) ) {
                ArgsNode paramnames = ParseParameterList ( false );
                ParseToken ( TokenType.Separator, "|" );
                return NODE_LINENO ( new BlockNode ( paramnames, ParseCommandList ( usebraces ) ), t );
            }

            return new BlockNode ( null, ParseCommandList ( usebraces ) );
        }

        private ExpressionListNode ParseCommandList ( bool usebraces = false ) {
            
            ExpressionListNode commands = new ExpressionListNode ();

            for ( t = lexer.NextToken (); t != null; t = lexer.NextToken () ) {
                if ( usebraces && t.Type == TokenType.Separator && t.Value == "}" )
                    
                    break;
                else if ( !usebraces && t.Type == TokenType.Identifier && t.Value == "end" )
                    break;

                if ( IsEndOfCommand ( t ) )
                    continue;

                lexer.PushToken ( t );
                commands.Push ( ParseCommand () );
            }

            lexer.PushToken ( t );

            if ( usebraces )
                ParseToken ( TokenType.Separator, "}" );
            else
                ParseName ( "end" );

            return commands;
        }

        private ExpressionListNode ParseCommandList ( IList< string > names ) {

            ExpressionListNode commands = new ExpressionListNode ();

            for ( t = lexer.NextToken ();
                t != null && ( t.Type != TokenType.Identifier || !names.Contains ( t.Value ) );
                t = lexer.NextToken () ) {
                
                if ( IsEndOfCommand ( t ) )
                    continue;

                lexer.PushToken ( t );
                commands.Push ( ParseCommand () );
            }

            lexer.PushToken ( t );

            return commands;
        }

        private void ParseEndOfCommand () {
            t = lexer.NextToken ();

            if ( t != null && t.Type == TokenType.Identifier && t.Value == "end" ) {
                lexer.PushToken ( t );
                return;
            }

            if ( t != null && t.Type == TokenType.Separator && t.Value == "}" ) {
                lexer.PushToken ( t );
                return;
            }

            if ( !IsEndOfCommand ( t ) ) {
                throw new SyntaxError ( "end of command expected", lexer.LexerPosition );
            }
        }
        
        private bool NextTokenStartsExpressionList () {
            
            t = lexer.NextToken ();
            lexer.PushToken ( t );

            if ( t == null )
                return false;

            if ( IsEndOfCommand ( t ) )
                return false;

            if ( t.Type == TokenType.Operator )
                return false;

            if ( t.Type == TokenType.Separator )
                return t.Value == "(";

            if ( t.Type == TokenType.Identifier && t.Value == "end" )
                return false;

            return true;
        }

        private bool IsEndOfCommand ( Token token ) {
            
            if ( token == null )
                return true;

            if ( token.Type == TokenType.EndOfLine )
                return true;

            if ( token.Type == TokenType.Separator && token.Value == ";" )
                return true;

            return false;
        }

        private AstNode ParseBinaryExpression ( int level ) {
            
            if ( level >= binaryoperators.Length )
                return ParseTerm ();

            AstNode expr = ParseBinaryExpression ( level + 1 );

            if ( expr == null ) {
				return null;
			}

            for ( t = lexer.NextToken (); t != null && IsBinaryOperator ( level, t ); t = lexer.NextToken () ) {

                switch ( t.Value ) {
                    case "+":
                    case "-":
                    case "*":
                    case "**":
                    case "/":
                    case "==":
                    case "===":
                    case "<=>":
                    case "!=":
                    case "<":
                    case ">":
                    case "<=":
                    case ">=":
                    case "<<":
                        expr = NODE_LINENO ( new CallNode ( expr, NODE_LINENO ( new SymNode ( t.Value ), t ), ParseBinaryExpression ( level + 1 ) ), t );
                        break;
                    case "..":
						expr = NODE_LINENO ( new Dot2Node ( expr, ParseBinaryExpression ( level + 1 ) ), t );
						break;
					case "...":
						expr = NODE_LINENO ( new Dot3Node ( expr, ParseBinaryExpression ( level + 1 ) ), t );
						break;
				}
            }

            if ( t != null ) {
				lexer.PushToken ( t );
			}

			return expr;
        }

        private AstNode ParseTerm () {
            
            AstNode node = null;

            if ( TryParseToken ( TokenType.Operator, "-" ) )
                node = NODE_LINENO ( new CallNode ( ParseTerm (), NODE_LINENO ( new SymNode ( "-@" ), t ), null ), t );
            else if ( TryParseToken ( TokenType.Operator, "+" ) )
                node = ParseTerm ();
            else if ( TryParseToken ( TokenType.Operator, "!" ) )
                node = NODE_LINENO ( new CallNode ( ParseTerm (), NODE_LINENO ( new SymNode ( "!" ), t ), null ), t );
            else
                node = ParseSimpleTerm ();

            if ( node == null ) {
				return null;
			}

			while ( true ) {

                if ( TryParseToken ( TokenType.Separator, "." ) ) {
                    string name = ParseName ();
                    SymNode named = NODE_LINENO ( new SymNode ( name ), t );

                    if ( TryParseToken ( TokenType.Separator, "{" ) ) {
                        node = NODE_LINENO ( new CallNode ( node, named, ParseBlockExpression ( true ) ), t );
                    }
                    else if ( TryParseToken ( TokenType.Operator, "=" ) ) { // A.method = something
                        var sym = NODE_LINENO ( new SymNode ( named.name + "=" ), t );
                        return NODE_LINENO ( new CallNode ( node, sym, ParseExpressionListAsArgs () ), t );
                    }
                    else if ( NextTokenStartsExpressionList () )
                        node = NODE_LINENO ( new CallNode ( node, named, ParseExpressionListAsArgs () ), t );
                    else
                        node = NODE_LINENO ( new CallNode ( node, named, null ), t );

                    continue;
                }

                if ( TryParseToken ( TokenType.Separator, "::" ) ) {
                    string name = ParseName ();
                    node = NODE_LINENO ( new Colon2Node ( node, NODE_LINENO ( new SymNode ( name ), t ) ), t );
                    continue;
                }

                if ( TryParseToken ( TokenType.Separator, "[" ) ) {
                    AstNode indexexpr = ParseExpression ();
                    ParseToken ( TokenType.Separator, "]" );
                    node = NODE_LINENO ( new ArrayRefNode ( node, indexexpr ), t );
                    continue;
                }

                break;
            }

            return node;
        }

        private AstNode ParseSimpleTerm () {
            
            t = lexer.NextToken ();
            Token token = t;

            if ( token == null )
                return null;

            if ( token.Type == TokenType.Integer )
                return NODE_LINENO ( new IntNode ( token.Value, false ), token );

            if ( token.Type == TokenType.Float )
                return NODE_LINENO ( new FloatNode ( token.Value ), token );

            if ( token.Type == TokenType.String )
                return NODE_LINENO ( new StringNode ( token.Value ), token );

            if ( token.Type == TokenType.Identifier ) {
                
                if ( token.Value == FALSE )
                    return NODE_LINENO ( new FalseNode ( token.Value ), token );

                if ( token.Value == TRUE )
                    return NODE_LINENO ( new TrueNode ( token.Value ), token );

                if ( token.Value == SELF )
                    return NODE_LINENO ( new SelfNode ( token.Value ), token );

                if ( token.Value == NIL )
                    return NODE_LINENO ( new NilNode ( null ), token );

				if ( token.Value == SUPER )
					return NODE_LINENO ( new SuperNode { args = ParseExpressionList () }, token );

				if ( token.Value == RETURN )
					return NODE_LINENO ( new ReturnNode { args = ParseExpressionList () }, token );

				if ( token.Value == YIELD )
					return NODE_LINENO ( new YieldNode { args = ParseExpressionList () }, token );

				if ( token.Value == BREAK )
					return NODE_LINENO ( new BreakNode (), token );

				if ( token.Value == DO )
                    return ParseBlockExpression ();

                if ( token.Value == IF )
                    return ParseIfExpression ();
                
                if ( token.Value == UNLESS )
                    return ParseUnlessExpression ();

                if ( token.Value == WHILE )
                    return ParseWhileExpression ();

                if ( token.Value == UNTIL )
                    return ParseUntilExpression ();

                if ( token.Value == FOR )
                    return ParseForInExpression ();

                if ( token.Value == DEF )
                    return ParseDefExpression ();

                if ( token.Value == CLASS )
                    return ParseClassExpression ();

                if ( token.Value == MODULE )
                    return ParseModuleExpression ();
                
                return NODE_LINENO ( new LocalVarNode ( token.Value ), token );
            }

            if ( token.Type == TokenType.InstanceVarName )
                return NODE_LINENO ( new InstanceVarNode ( token.Value ), token );

            if ( token.Type == TokenType.ClassVarName )
                return NODE_LINENO ( new ClassVarNode ( token.Value ), token );
            
            if ( token.Type == TokenType.GlobalVarName )
                return NODE_LINENO ( new GlobalVarNode  ( token.Value ), token );
            
            if ( token.Type == TokenType.ConstantVarName )
                return NODE_LINENO ( new ConstantNode ( token.Value ), token );

            if ( token.Type == TokenType.Symbol )
                return NODE_LINENO ( new SymbolNode ( token.Value ), token );

            if ( token.Type == TokenType.Separator && token.Value == "(" ) {
                AstNode expr = ParseExpression ();
                ParseToken ( TokenType.Separator, ")" );
                return expr;
            }

            if ( token.Type == TokenType.Separator && token.Value == "{" ) {
                return ParseHashExpression ();
            }

            if ( token.Type == TokenType.Separator && token.Value == "[" ) {
                IList< AstNode > expressions = ParseExpressionList ( "]" );
                return NODE_LINENO ( new ArrayNode ( expressions ), token );
            }

            lexer.PushToken ( token );

            return null;
        }

        private HashNode ParseHashExpression () {
            
            IList< AstNode > keyexpressions   = new List< AstNode > ();
            IList< AstNode > valueexpressions = new List< AstNode > ();

            Token token = null;

            while ( !TryParseToken ( TokenType.Separator, "}" ) ) {

                token = t;

                if ( keyexpressions.Count > 0 )
                    ParseToken ( TokenType.Separator, "," );

                var keyexpression = ParseExpression ();
                ParseToken ( TokenType.Operator, "=>" );
                var valueexpression = ParseExpression ();

                keyexpressions.Add ( keyexpression );
                valueexpressions.Add ( valueexpression );
            }

            return NODE_LINENO ( new HashNode ( keyexpressions, valueexpressions ), token );
        }

        private AstNode ParseNamePathExpression () {

			string  name  = ParseName ();
			AstNode named = null;
            // NamePathNode namePathNode = NODE_LINENO ( new NamePathNode (), t );
            
            if ( name == SELF ) {
                named = NODE_LINENO ( new SelfNode ( SELF ), t );
            }
            else if ( char.IsUpper ( name[ 0 ] ) ) {
                named = NODE_LINENO ( new ConstantNode ( name ), t );
            }
            else {
                named = NODE_LINENO ( new SymNode ( name ), t );
            }

            // namePathNode.Push ( named );

            while ( true ) {
                
				if ( TryParseToken ( TokenType.Separator, "." ) ) {
                    //namePathNode.Push ( NODE_LINENO ( new DotNode ( "." ), t ) );
                    string newname = ParseName ( out t );
                    // namePathNode.Push ( NODE_LINENO ( new SymNode ( newname ), t ) );
                    named = NODE_LINENO ( new DotNode ( named, NODE_LINENO ( new SymNode ( newname ), t ) ), t );
                    continue;
				}

				if ( TryParseToken ( TokenType.Separator, "::" ) ) {
                    //namePathNode.Push ( NODE_LINENO ( new Colon2Node ( "::" ), t ) );
                    string newname = ParseName ( out t );
                    // namePathNode.Push ( NODE_LINENO ( new SymNode ( newname ), t ) );
                    named = NODE_LINENO ( new Colon2Node ( named, NODE_LINENO ( new SymNode ( newname ), t ) ), t );
                    continue;
				}

				break;
			}

            return named;
        }
        
        private AstNode ParseFunctionNamePathExpression () {

            string  name  = ParseFunctionName ();
            AstNode named = null;
            
            if ( name == SELF ) {
                named = NODE_LINENO ( new SelfNode ( SELF ), t );
            }
            else if ( char.IsUpper ( name[ 0 ] ) ) {
                named = NODE_LINENO ( new ConstantNode ( name ), t );
            }
            else {
                named = NODE_LINENO ( new SymNode ( name ), t );
            }
            
            while ( true ) {
                
                if ( TryParseToken ( TokenType.Separator, "." ) ) {
                    string newname = ParseFunctionName ( out t );
                    named = NODE_LINENO ( new DotNode ( named, NODE_LINENO ( new SymNode ( newname ), t ) ), t );
                    continue;
                }

                if ( TryParseToken ( TokenType.Separator, "::" ) ) {
                    string newname = ParseFunctionName ( out t );
                    named = NODE_LINENO ( new Colon2Node ( named, NODE_LINENO ( new SymNode ( newname ), t ) ), t );
                    continue;
                }

                break;
            }

            return named;
        }

        private void ParseName ( string name ) {
            ParseToken ( TokenType.Identifier, name );
        }

        private void ParseToken ( TokenType type, string value ) {
            t = lexer.NextToken ();

            if ( t == null || t.Type != type || t.Value != value )
                throw new SyntaxError ( string.Format ( "expected '{0}'", value ), lexer.LexerPosition );
        }

        private string ParseName () {
            t = lexer.NextToken ();

			if ( t == null ||
				 ( t.Type != TokenType.Identifier &&
				   t.Type != TokenType.ConstantVarName ) ) {
				throw new SyntaxError ( $"name expected, but get {t}", lexer.LexerPosition );
			}

			return t.Value;
        }
        
        private string ParseName ( out Token token ) {
            t = lexer.NextToken ();
            token = t;

            if ( token == null || 
                 ( token.Type != TokenType.Identifier &&
                   token.Type != TokenType.ConstantVarName ) ) {
                throw new SyntaxError ( $"name expected, but get {token}", lexer.LexerPosition );
            }

            return token.Value;
        }
        
        private string ParseFunctionName () {
            t = lexer.NextToken ();
            Token named = t;

            if ( t == null ||
                 ( t.Type != TokenType.Identifier &&
                   t.Type != TokenType.ConstantVarName ) ) {
                throw new SyntaxError ( $"name expected, but get {t}", lexer.LexerPosition );
            }

            if ( TryParseToken ( TokenType.Operator, "=" ) ) {
                return named.Value + "=";
            }

            return named.Value;
        }
        
        private string ParseFunctionName ( out Token token ) {
            t     = lexer.NextToken ();
            token = t;

            if ( token == null || 
                 ( token.Type != TokenType.Identifier &&
                   token.Type != TokenType.ConstantVarName ) ) {
                throw new SyntaxError ( $"name expected, but get {token}", lexer.LexerPosition );
            }
            
            if ( TryParseToken ( TokenType.Operator, "=" ) ) {
                token = t;
                return token.Value + "=";
            }

            return token.Value;
        }

        private bool TryParseName ( string name ) {
            return TryParseToken ( TokenType.Identifier, name );
        }
        
        private bool TryParseToken ( TokenType type, string value ) {
            t = lexer.NextToken ();

            if ( t != null && t.Type == type && t.Value == value )
                return true;

            lexer.PushToken ( t );

            return false;
        }
        
        private string TryParseName () {
            t = lexer.NextToken ();

            if ( t != null && t.Type == TokenType.Identifier )
                return t.Value;

            lexer.PushToken ( t );

            return null;
        }

		private string TryParseName ( out Token token ) {
			t = lexer.NextToken ();
			token = t;

			if ( token != null && token.Type == TokenType.Identifier )
				return token.Value;

			lexer.PushToken ( token );

			return null;
		}

		private bool TryParseEndOfLine () {
            t = lexer.NextToken ();

            if ( t != null && t.Type == TokenType.EndOfLine && t.Value == "\n" )
                return true;

            lexer.PushToken ( t );

            return false;
        }

        private bool IsBinaryOperator ( int level, Token token ) {
            return token.Type == TokenType.Operator && binaryoperators[ level ].Contains ( token.Value );
        }

        private T NODE_LINENO< T > ( T node, Token token ) where T : AstNode {
            if ( node != null ) {
                node.filename = filepath;
                if ( token != null ) {
                    node.lineno = token.span.Start.Line;
                }
            }

            return node;
        }

        #region Static Method

        /// <summary>
        /// such as "Ruby.Core.Compiler.AstParser", return "AstParser"
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static string GetNamePathFinalName ( AstNode node ) {
            AstNode root = node;
            string  name = string.Empty;
            while ( root != null ) {
                switch ( root.type ) {
                    case AstNodeType.SYM:
                    case AstNodeType.CONST:
                        name = ( ( INamedNode )root ).name;
                        root = null;
                        break;
                    case AstNodeType.SELF:
                        root = null;
                        name = "self"; // shouldn't happen.
                        break;
                    case AstNodeType.DOT:
                        root = ( root as DotNode ).cdr;
                        break;
                    case AstNodeType.COLON2:
                        root = ( root as Colon2Node ).cdr;
                        break;
                    default:
                        Console.WriteLine ( $"AstParser::GetNamePathFinalName() not support: {root.type}" );
                        root = null;
                        break;
                }
            }

            return name;
        }

        #endregion
    }


    /// <summary>
    /// parser structure
    /// </summary>
    public class AstParserState {
        public VM      vm;
        public AstNode tree;
        public string  filename;
        public bool    on_eval;
    }
}
