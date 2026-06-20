parser grammar PlayscriptContentParser;

options { tokenVocab = PlayscriptContentLexer; }

@header { namespace EasyPlayscript.Parsing; }

// ─── Entry Points ───────────────────────────────────────────────────────────

scriptContent
    : page (pageBreak page)* EOF
    ;

textContent
    : textParagraph (BLANK_LINE textParagraph)* EOF
    ;

// ─── Content Structure ──────────────────────────────────────────────────────

page
    : paragraph (BLANK_LINE paragraph)*
    ;

paragraph
    : line (SINGLE_NEWLINE line)*
    ;

line
    : (TEXT | consumerCall)+
    ;

textParagraph
    : textLine (SINGLE_NEWLINE textLine)*
    ;

textLine
    : (TEXT | SLASH | consumerCall)+
    ;

pageBreak
    : (SINGLE_NEWLINE | BLANK_LINE)* SLASH (SINGLE_NEWLINE | BLANK_LINE)*
    ;

consumerCall
    : AT IDENTIFIER LPAREN (argument (COMMA argument)*)? RPAREN
    ;

argument
    : STRING_LITERAL
    | INTEGER_LITERAL
    | FLOAT_LITERAL
    | BOOLEAN_LITERAL
    ;
