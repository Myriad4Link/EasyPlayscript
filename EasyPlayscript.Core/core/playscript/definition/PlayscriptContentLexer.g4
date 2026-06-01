lexer grammar PlayscriptContentLexer;

@header { namespace EasyPlayscript.Parsing; }

tokens { AT, COMMENT, LPAREN, RPAREN, STRING_LITERAL, IDENTIFIER }

// ─── DEFAULT MODE (content inside [...]) ─────────────────────────────────────

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

SLASH
    : '/'
    ;

TEXT
    : ~[@\]\r\n#/]+
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
