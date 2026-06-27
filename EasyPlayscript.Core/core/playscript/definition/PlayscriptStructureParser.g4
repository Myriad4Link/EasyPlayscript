parser grammar PlayscriptStructureParser;

options { tokenVocab = PlayscriptStructureLexer; }

@header { namespace EasyPlayscript.Parsing; }

playscript  : topLevelStatement* EOF ;

topLevelStatement
    : blockType IDENTIFIER LBRACKET RAW_CONTENT RBRACKET
    | ASYNC? INTERFACE IDENTIFIER LPAREN paramList? RPAREN COLON typeSpec
    ;

blockType   : SCRIPT | TEXT ;

paramList   : parameter (COMMA parameter)* ;
parameter   : IDENTIFIER COLON typeSpec ;
typeSpec    : STRING_TYPE | INT_TYPE | DECIMAL_TYPE | BOOL_TYPE | VOID_TYPE ;
