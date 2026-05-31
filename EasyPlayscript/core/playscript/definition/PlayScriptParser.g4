parser grammar PlayscriptParser;

options { tokenVocab = PlayscriptLexer; }

@header { namespace EasyPlayscript.Parsing; }

// ─── Entry Point ────────────────────────────────────────────────────────────

playscript
    : statement* EOF
    ;

// ─── Top-Level Statements ───────────────────────────────────────────────────

statement
    : compilerCall scriptBlock?
    ;

compilerCall
    : DOT IDENTIFIER LPAREN STRING_LITERAL RPAREN
    ;

scriptBlock
    : LBRACKET scriptContent* RBRACKET
    ;

// ─── Script Block Content ───────────────────────────────────────────────────

scriptContent
    : sentence
    | consumerCall
    | BLANK_LINE
    | SINGLE_NEWLINE
    ;

sentence
    : sentencePart (SINGLE_NEWLINE sentencePart)*
    ;

sentencePart
    : TEXT+
    ;

consumerCall
    : AT IDENTIFIER LPAREN STRING_LITERAL RPAREN
    ;
