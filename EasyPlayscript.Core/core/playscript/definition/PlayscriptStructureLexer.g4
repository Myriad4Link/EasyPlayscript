lexer grammar PlayscriptStructureLexer;

@header { namespace EasyPlayscript.Parsing; }

SCRIPT      : 'script' ;
TEXT        : 'text' ;
ASYNC       : 'async' ;
INTERFACE   : 'interface' ;

LBRACKET    : '[' -> pushMode(IN_RAW) ;

COLON       : ':' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
COMMA       : ',' ;

STRING_TYPE : 'string' ;
INT_TYPE    : 'int' ;
DECIMAL_TYPE: 'decimal' ;
BOOL_TYPE   : 'bool' ;
VOID_TYPE   : 'void' ;

IDENTIFIER  : [a-zA-Z_] [a-zA-Z0-9_]* ;

WS          : [ \t]+ -> skip ;
NEWLINE     : '\r'? '\n' -> skip ;
COMMENT     : '#' ~[\r\n]* -> skip ;

mode IN_RAW;

RAW_CONTENT : ~[\]]+ ;
RBRACKET    : ']' -> popMode ;
