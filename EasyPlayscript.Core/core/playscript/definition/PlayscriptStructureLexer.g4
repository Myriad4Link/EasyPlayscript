lexer grammar PlayscriptStructureLexer;

@header { namespace EasyPlayscript.Parsing; }

DOT         : '.' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
LBRACKET    : '[' -> pushMode(IN_RAW) ;
IDENTIFIER  : [a-zA-Z_] [a-zA-Z0-9_]* ;
STRING_LITERAL : '"' (~["\\\r\n] | '\\' .)* '"' ;

WS          : [ \t]+ -> skip ;
NEWLINE     : '\r'? '\n' -> skip ;
COMMENT     : '#' ~[\r\n]* -> skip ;

mode IN_RAW;

RAW_CONTENT : ~[\]]+ ;
RBRACKET    : ']' -> popMode ;
