lexer grammar PlayscriptStructureLexer;

@header { namespace EasyPlayscript.Parsing; }

LBRACKET    : '[' -> pushMode(IN_RAW) ;
IDENTIFIER  : [a-zA-Z_] [a-zA-Z0-9_]* ;

WS          : [ \t]+ -> skip ;
NEWLINE     : '\r'? '\n' -> skip ;
COMMENT     : '#' ~[\r\n]* -> skip ;

mode IN_RAW;

RAW_CONTENT : ~[\]]+ ;
RBRACKET    : ']' -> popMode ;
