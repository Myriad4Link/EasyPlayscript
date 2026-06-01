parser grammar PlayscriptStructureParser;

options { tokenVocab = PlayscriptStructureLexer; }

@header { namespace EasyPlayscript.Parsing; }

playscript   : statement* EOF ;
statement    : compilerCall scriptBlock? ;
compilerCall : DOT IDENTIFIER LPAREN STRING_LITERAL RPAREN ;
scriptBlock  : LBRACKET RAW_CONTENT RBRACKET ;
