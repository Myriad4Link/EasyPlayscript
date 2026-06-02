parser grammar PlayscriptStructureParser;

options { tokenVocab = PlayscriptStructureLexer; }

@header { namespace EasyPlayscript.Parsing; }

playscript : statement* EOF ;
statement  : blockType IDENTIFIER LBRACKET RAW_CONTENT RBRACKET ;
blockType  : SCRIPT | TEXT ;
