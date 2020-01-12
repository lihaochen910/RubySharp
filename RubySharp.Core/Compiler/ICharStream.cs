namespace RubySharp.Core.Compiler
{
    using System;


    public interface ICharStream {
	    void BackChar ();

	    int NextChar ();

	    int PeekChar ( int i );

	    int Position ();

	    string Data ();
    }
}
