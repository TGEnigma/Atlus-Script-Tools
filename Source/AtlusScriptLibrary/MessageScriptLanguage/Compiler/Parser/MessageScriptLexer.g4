lexer grammar MessageScriptLexer;

////////////////////
//
// Lexer rules
//
////////////////////

//
// Text lexer rules
//

OpenCode
	: '[' -> pushMode( MessageScriptCode );

CloseText
	: ']' -> pushMode( MessageScriptCode );  // close tag is used for closing inline text inside tag

// match line comment starting with //
LineComment
    : '//' ~[\r\n]* '\r'? '\n'?
		-> skip
	;

// match actual text
Text
	: ~( '[' | ']' | '/' )+ // can't match full line comment, but matching first character is sufficient
	| '/' ~[/]				// match single slash as text (workaround for excluding comment)
	;

//
// Code lexer rules
//
mode MessageScriptCode;

MessageDialogTagId
	: 'msg'
	| 'dlg'
	;

SelectionDialogTagId
	: 'sel';

SelectionDialogPatternId
	: 'top'
	| 'bottom'
	;

// Keywords
CloseCode
	: ']' -> popMode;

OpenText
	: '[' -> popMode;  // open tag is used for opening inline text inside tag

// Literals
fragment
IdentifierEscape: '``';

// This must come before identifier, otherwise some integers 
// will get mistaken for an identifier
IntLiteral
	: ( DecIntLiteral | HexIntLiteral );

Identifier
	: ( Letter | '_' ) ( Letter | '_' | Digit )*		// C style identifier
	| IdentifierEscape ( ~( '`' ) )* IdentifierEscape	// Verbatim string identifier for otherwise invalid names
	;

fragment
DecIntLiteral
	: Sign? Digit+;

fragment
HexIntLiteral
	: Sign? HexLiteralPrefix HexDigit+;

fragment
Letter
	: ( [a-zA-Z] );

fragment
Digit
	: [0-9];

fragment
HexDigit
	: ( Digit | [a-fA-F] );

fragment
HexLiteralPrefix
	: '0' [xX];

fragment
Sign
	: '+' | '-';

Whitespace
	: [ \t\r\n] -> skip;
