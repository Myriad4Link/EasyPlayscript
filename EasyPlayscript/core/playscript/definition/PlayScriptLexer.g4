lexer grammar PlayscriptLexer;

@header { namespace EasyPlayscript.Parsing; }

// ─── DEFAULT MODE ────────────────────────────────────────────────────────────

COMMENT
    : '#' ~[\r\n]* -> channel(HIDDEN)
    ;

AT          : '@' ;
LPAREN      : '(' ;
RPAREN      : ')' ;
LBRACKET    : '[' -> pushMode(IN_SCRIPT) ;
RBRACKET    : ']' ;

IDENTIFIER
    : [a-zA-Z_] [a-zA-Z0-9_]*
    ;

STRING_LITERAL
    : '"' (~["\\\r\n] | '\\' .)* '"'
    ;

WS          : [ \t]+ -> skip ;
NEWLINE     : '\r'? '\n' -> skip ;

// ─── IN_SCRIPT MODE ─────────────────────────────────────────────────────────

mode IN_SCRIPT;

S_RBRACKET
    : ']' -> type(RBRACKET), popMode
    ;

S_AT
    : '@' -> type(AT), pushMode(IN_CALL)
    ;

S_COMMENT
    : '#' ~[\r\n]* -> type(COMMENT), channel(HIDDEN)
    ;

BLANK_LINE
    : '\r'? '\n' [ \t]* '\r'? '\n'
    ;

SINGLE_NEWLINE
    : '\r'? '\n'
    ;

TEXT
    : ~[@\]\r\n#]+
    ;

// ─── IN_CALL MODE ───────────────────────────────────────────────────────────

mode IN_CALL;

C_LPAREN
    : '(' -> type(LPAREN)
    ;

C_RPAREN
    : ')' -> type(RPAREN), popMode
    ;

C_STRING_LITERAL
    : '"' (~["\\\r\n] | '\\' .)* '"' -> type(STRING_LITERAL)
    ;

C_IDENTIFIER
    : [a-zA-Z_] [a-zA-Z0-9_]* -> type(IDENTIFIER)
    ;

C_WS
    : [ \t]+ -> skip
    ;
