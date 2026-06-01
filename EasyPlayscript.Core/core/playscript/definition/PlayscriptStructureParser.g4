parser grammar PlayscriptStructureParser;

options { tokenVocab = PlayscriptStructureLexer; }

@header { namespace EasyPlayscript.Parsing; }

playscript : statement* EOF ;
statement  : IDENTIFIER IDENTIFIER LBRACKET RAW_CONTENT RBRACKET ;
