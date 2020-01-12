namespace RubySharp.Tests
{
	using System;
	using System.Collections.Generic;
	using RubySharp.Core;
	using RubySharp.Core.Compiler;
	using RubySharp.Core.Expressions;


	public class Program {
		
		public static void Main ( string[] args ) {
			
			Machine machine = new Machine ();

			foreach ( var arg in args )
				machine.ExecuteFile ( arg );

			machine.ExecuteFile ( @"/Users/Kanbaru/OneDrive/文档/CocoR/CocoRuby/test_0.rb" );
		}
	}
}