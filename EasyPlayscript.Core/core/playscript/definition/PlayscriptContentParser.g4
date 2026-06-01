parser grammar PlayscriptContentParser;

options { tokenVocab = PlayscriptContentLexer; }

@header { namespace EasyPlayscript.Parsing; }

// ─── Entry Point ────────────────────────────────────────────────────────────

scriptContent
    : page (pageBreak page)* EOF
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

pageBreak
    : (SINGLE_NEWLINE | BLANK_LINE)* SLASH (SINGLE_NEWLINE | BLANK_LINE)*
    ;

consumerCall
    : AT IDENTIFIER LPAREN STRING_LITERAL RPAREN
    ;
