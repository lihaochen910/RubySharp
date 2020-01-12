namespace RubySharp.Core.Compiler
{
    public enum TokenType
    {
        /// <summary>
        /// 标识符
        /// </summary>
        Identifier = 1,
                
        /// <summary>
        /// 整数值
        /// </summary>
        Integer = 2,
        
        /// <summary>
        /// 浮点数
        /// </summary>
        Float = 3,
                
        /// <summary>
        /// 字符串
        /// </summary>
        String = 4,
        
        /// <summary>
        /// 操作符
        /// </summary>
        Operator = 5,
        
        /// <summary>
        /// 分隔符
        /// </summary>
        Separator = 6,
        
        /// <summary>
        /// Ruby Symbol
        /// </summary>
        Symbol = 7,
        
        /// <summary>
        /// 类实例变量
        /// </summary>
        InstanceVarName = 8,
        
        /// <summary>
        /// 类变量
        /// </summary>
        ClassVarName = 9,
        
        /// <summary>
        /// 全局变量
        /// </summary>
        GlobalVarName = 10,
        
        /// <summary>
        /// 常量
        /// </summary>
        ConstantVarName = 11,
        
        /// <summary>
        /// 换行符
        /// </summary>
        EndOfLine = 12
    }
}
