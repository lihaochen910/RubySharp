namespace RubySharp.Console
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using RubySharp.Core;
    using RubySharp.Core.Compiler;
    
    
    public class Program {
	    public static void Main ( string[] args ) {

		    //Machine machine = new Machine ();

		    //foreach ( var arg in args )
		    //    machine.ExecuteFile ( arg );

		    //machine.ExecuteFile ( @"/Users/Kanbaru/OneDrive/文档/CocoR/CocoRuby/test_0.rb" );

		    // foreach ( var path in System.IO.Directory.GetFiles ( @"/Users/Kanbaru/OneDrive/文档/RubySharp/Src/RubySharp.Core.Tests/MachineFiles" ) ) {
			foreach ( var path in System.IO.Directory.GetFiles ( @"D:/OneDrive/文档/RubySharp/Src/RubySharp.Core.Tests/MachineFiles" ) ) {

			    if ( System.IO.Path.GetExtension ( path ) != ".rb" ) {
				    continue;
			    }

			    if ( !path.EndsWith ( "main.rb" ) ) {
				    continue;
			    }

			    Console.WriteLine ( path );

			    AstParser aparser = new AstParser ( System.IO.File.ReadAllText ( path ) );
			    aparser.filepath = path;
//				for ( var command = aparser.ParseCommand (); command != null; command = aparser.ParseCommand () ) {
//					Console.WriteLine ( command );
//				}
			    var vm = new VM ();
			    
			    UserDataUtility.RegAssembly ( vm, typeof ( Microsoft.Xna.Framework.Game ).Assembly );
			    // UserDataUtility.RegCustomClass ( vm, typeof ( Microsoft.Xna.Framework.Vector2 ) );
			    // UserDataUtility.RegCustomClass ( vm, typeof ( Microsoft.Xna.Framework.Curve ) );
			    
			    vm.Evaluate ( aparser.Parse () );
			    
			    Console.WriteLine ();
		    }



		    //if ( args.Length == 0 ) {
		    //    Console.WriteLine ( "rush 0.0.1-alpha-alpha-alpha-realpha ;-)" );

		    //    Parser parser = new Parser ( Console.In );

		    //    while ( true )
		    //        try {
		    //            IExpression expr   = parser.ParseCommand ();
		    //            var         result = expr.Evaluate ( machine.RootContext );
		    //            var         text   = result == null ? "nil" : result.ToString ();
		    //            Console.WriteLine ( string.Format ( "=> {0}", text ) );
		    //        }
		    //        catch ( Exception ex ) {
		    //            Console.WriteLine ( ex.Message );
		    //            Console.WriteLine ( ex.StackTrace );
		    //        }
		    //}
	    }
    }
}
