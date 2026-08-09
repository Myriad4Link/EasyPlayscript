parser grammar PlayscriptStructureParser;

options { tokenVocab = PlayscriptStructureLexer; }

@header { namespace EasyPlayscript.Parsing; }

playscript  : namespaceDeclaration? topLevelStatement* EOF ;

namespaceDeclaration
    : NAMESPACE IDENTIFIER (DOT IDENTIFIER)*
    ;

topLevelStatement
    : blockType IDENTIFIER (DEFAULT? VARIATION IDENTIFIER)? LBRACKET RAW_CONTENT RBRACKET
    | ASYNC? INTERFACE IDENTIFIER LPAREN paramList? RPAREN COLON typeSpec
    ;

blockType   : SCRIPT | TEXT ;

paramList   : parameter (COMMA parameter)* ;
parameter   : IDENTIFIER COLON typeSpec ;
typeSpec    : STRING_TYPE | INT_TYPE | DECIMAL_TYPE | BOOL_TYPE | VOID_TYPE ;
