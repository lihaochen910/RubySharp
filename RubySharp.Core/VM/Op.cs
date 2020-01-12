using System;


namespace RubySharp.Core {

	public class Instr {

		private static readonly Instr nopInstr = new Instr ( OpCode.Nop );
		public static Instr Nop {
			get => nopInstr;
		}

		public OpCode insn;
		public int a = Int32.MinValue;
		public int b = Int32.MinValue;
		public int c = Int32.MinValue;

		public Instr ( OpCode op ) {
			insn = op;
		}
		
		public Instr ( OpCode op, int a ) {
			insn = op;
			this.a = a;
		}
		
		public Instr ( OpCode op, int a, int b ) {
			insn   = op;
			this.a = a;
			this.b = b;
		}
		
		public Instr ( OpCode op, int a, int b, int c ) {
			insn   = op;
			this.a = a;
			this.b = b;
			this.c = b;
		}

		public override string ToString () {
			bool hasA = a != int.MinValue;
			bool hasB = b != int.MinValue;
			bool hasC = c != int.MinValue;
			return $"{insn}" + ( hasA ? $" {a}" : string.Empty ) + ( hasB ? $" {b}" : string.Empty ) +
			       ( hasC ? $" {c}" : string.Empty );
		}
	}
	
	public enum OpCode : byte {
		Nop,      /* no operation */
		Move,     /* R(a) = R(b) */
		LoadL,    /* R(a) = Pool(b) */
		LoadI,    /* R(a) = mrb_int(b) */
		LoadSYM,  /* R(a) = Syms(b) */
		LoadNIL,  /* R(a) = nil */
		LoadSelf, /* R(a) = self */
		LoadT,    /* R(a) = true */
		LoadF,    /* R(a) = false */
		GetGV,    /* R(a) = getglobal(Syms(b)) */
		SetGV,    /* setglobal(Syms(b), R(a)) */
		GetIV,    /* R(a) = ivget(Syms(b)) */
		SetIV,    /* ivset(Syms(b),R(a)) */
		GetCV,    /* R(a) = cvget(Syms(b)) */
		SetCV,    /* cvset(Syms(b),R(a)) */
		GetConst, /* R(a) = constget(Syms(b)) */
		SetConst, /* constset(Syms(b),R(a)) */
		GetUpVar, /* R(a) = uvget(b,c) */
		SetUpVar, /* uvset(b,c,R(a)) */
		
		Jmp,       /* pc=a */
		JmpIf,     /* if R(b) pc=a */
		JmpNot,    /* if !R(b) pc=a */
		JmpNil,    /* if R(b)==nil pc=a */
		Raise,     /* raise(R(a)) */
		SendV,     /* R(a) = call(R(a),Syms(b),*R(a+1)) */
		SendVB,    /* R(a) = call(R(a),Syms(b),*R(a+1),&R(a+2)) */
		Send,	   /* R(a) = call(R(a),Syms(b),R(a+1),...,R(a+c)) */
		SendB,     /* R(a) = call(R(a),Syms(Bx),R(a+1),...,R(a+c),&R(a+c+1)) */
		Call,      /* R(0) = self.call(frame.argc, frame.argv) */
		Super,     /* R(a) = super(R(a+1),... ,R(a+b+1)) */
		Return,    /* return R(a) (normal) */
		ReturnBlk, /* return R(a) (in-block return) */
		Break,     /* break R(a) */
		
		Add,       /* R(a) = R(a)+R(a+1) */
		AddI,      /* R(a) = R(a)+mrb_int(c)  */
		Sub,       /* R(a) = R(a)-R(a+1) */
		SubI,      /* R(a) = R(a)-C */
		Mul,       /* R(a) = R(a)*R(a+1) */
		Div,       /* R(a) = R(a)/R(a+1) */
		EQ,        /* R(a) = R(a)==R(a+1) */
		LT,        /* R(a) = R(a)<R(a+1) */
		LE,        /* R(a) = R(a)<=R(a+1) */
		GT,        /* R(a) = R(a)>R(a+1) */
		GE,        /* R(a) = R(a)>=R(a+1) */
		
		Array,    /* R(a) = ary_new(R(a),R(a+1)..R(a+b)) */
		AryCat,   /* ary_cat(R(a),R(a+1)) */
		AryPush,  /* ary_push(R(a),R(a+1)) */
		AryDup,   /* R(a) = ary_dup(R(a)) */
		ARef,     /* R(a) = R(b)[c] */
		ASet,     /* R(a)[c] = R(b) */
		APost,    /* *R(a),R(a+1)..R(a+c) = R(a)[b..] */
		
		Intern,   /* R(a) = intern(R(a)) */
		String,   /* R(a) = str_dup(Lit(b)) */
		StrCat,   /* str_cat(R(a),R(a+1)) */
		
		Hash,     /* R(a) = hash_new(R(a),R(a+1)..R(a+b)) */
		HashAdd,  /* R(a) = hash_push(R(a),R(a+1)..R(a+b)) */
		HashCat,  /* R(a) = hash_cat(R(a),R(a+1)) */
		
		Lambda,   /* R(a) = lambda(SEQ[b],L_LAMBDA) */
		Block,    /* R(a) = lambda(SEQ[b],L_BLOCK) */
		Method,   /* R(a) = lambda(SEQ[b],L_METHOD) */
		RangeInc, /* R(a) = range_new(R(a),R(a+1),FALSE) */
		RangeExc, /* R(a) = range_new(R(a),R(a+1),TRUE) */
		OClass,   /* R(a) = ::Object */
		Class,    /* R(a) = newclass(R(a),Syms(b),R(a+1)) */
		Module,   /* R(a) = newmodule(R(a),Syms(b)) */
		Def,      /* R(a).newmethod(Syms(b),R(a+1)) */
		Alias,    /* alias_method(target_class,Syms(a),Syms(b)) */
		Undef,    /* undef_method(target_class,Syms(a)) */
		SClass,   /* R(a) = R(a).singleton_class */
		TClass,   /* R(a) = target_class */

		EXEC,     /* R(a) = blockexec(R(a),SEQ[b]) */

		Debug,    /* print a,b,c */
		Err,      /* raise(LocalJumpError, Lit(a)) */
		Stop,     /* stop VM */
	}
}