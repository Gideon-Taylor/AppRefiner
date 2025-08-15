//******************************************************************************
//* ANTLR 4 LEXER GRAMMAR FOR PEOPLECODE PROGRAMS AND APPLICATION CLASSES
//* by Leandro Baca
//******************************************************************************
//* NOTE: This version contains Java-specific semantic predicates.
//******************************************************************************

lexer grammar PeopleCodeLexer;

options {
        language = CSharp;
        superClass = ByteTrackingLexerBase;
}

channels {
        API_COMMENTS,
        COMMENTS,
        DIRECTIVES,
        WHITESPACE
}

@header {
using System.Text.RegularExpressions;
using AppRefiner.PeopleCode;
}

@members {
	private static Regex REGEX_REM = new Regex("^rem(ark)?\\b.*",RegexOptions.IgnoreCase | RegexOptions.Singleline);
	private static Regex REGEX_3_STAR = new Regex("^/\\*{3}/.*", RegexOptions.Singleline);
}


//******************************************************************************
//* DEFAULT MODE
//******************************************************************************

BLOCK_COMMENT_SLASH
        :       (
                        '/*' ~[*] .*? '*/'
                |       '/*' '*'* '*/'
                ) -> channel(COMMENTS)
        ;

API_COMMENT : '/**' ~[/] .*? '*/' {!REGEX_3_STAR.IsMatch(Text)}? -> channel(API_COMMENTS) ;
WS : [\p{White_Space}]+ -> channel(WHITESPACE) ;
BLOCK_COMMENT_NEST : '<*' (BLOCK_COMMENT_NEST | .)*? '*>' -> channel(COMMENTS) ;

//BLOCK_COMMENT_PLUS : '/+' .*? '+/' ';'? -> channel(COMMENTS) ;

LINE_COMMENT : R E M (A R K)? .*? ';' {REGEX_REM.IsMatch(Text)}? -> channel(COMMENTS) ;
DIR_IF : '#' IF -> channel(DIRECTIVES), pushMode(DIRECTIVE) ;
DIR_ELSE : '#' ELSE SEMI* -> channel(DIRECTIVES) ;
DIR_END_IF : '#' END IF SEMI* -> channel(DIRECTIVES) ;

ABSTRACT : A B S T R A C T ;
ADD : '+' ;
ALIAS : A L I A S ;
AND : A N D ;
ARRAY : A R R A Y ;
ARRAY2 : A R R A Y '2' ;
ARRAY3 : A R R A Y '3' ;
ARRAY4 : A R R A Y '4' ;
ARRAY5 : A R R A Y '5' ;
ARRAY6 : A R R A Y '6' ;
ARRAY7 : A R R A Y '7' ;
ARRAY8 : A R R A Y '8' ;
ARRAY9 : A R R A Y '9' ;
AS : A S ;
AT : '@' ;
BREAK : B R E A K ;
CATCH : C A T C H ;
CLASS : C L A S S ;
COLON : ':' ;
COMMA : ',' ;
SLASH_PLUS : '/+';
PLUS_SLASH : '+/';
COMPONENT
	:	C O M P O N E N T L I F E
	|	C O M P O N E N T
	|	P A N E L G R O U P
	;

CONSTANT : C O N S T A N T ;
CONTINUE : C O N T I N U E ;
CREATE : C R E A T E ;
DECLARE : D E C L A R E ;
DOC : D O C ;
DIV : '/' ;
DOT : '.' ;
ELSE : E L S E ;
END_CLASS : END CLASS ;
END_EVALUATE : END EVALUATE ;
END_FOR : END FOR ;
END_GET : END GET ;
END_IF : END IF ;
END_INTERFACE : END INTERFACE ;
END_METHOD : END METHOD ;
END_SET : END SET ;
END_TRY : END TRY ;
END_WHILE : END WHILE ;
EQ : '=' ;
ERROR : E R R O R ;
EVALUATE : E V A L U A T E ;
EXCEPTION : E X C E P T I O N ;
EXIT : E X I T ;
EXP : '**' ;
EXTENDS : E X T E N D S ;
FOR : F O R ;
FUNCTION : F U N C T I O N ;
END_FUNCTION : END FUNCTION ;
GE : '>=' ;
GET : G E T ;
GLOBAL : G L O B A L ;
GT : '>' ;
IF : I F ;
IMPLEMENTS : I M P L E M E N T S ;
IMPORT : I M P O R T ;
INSTANCE : I N S T A N C E ;
INTERFACE : I N T E R F A C E ;
INTEGER : I N T E G E R ;
LBRACKET : '[' ;
LE : '<=' ;
LIBRARY : L I B R A R Y ;
LOCAL : L O C A L ;
LPAREN : '(' ;
LT : '<' ;
METHOD : M E T H O D ;

NEQ
	:	'<>'
	|	'!='
	;

NOT : N O T ;
NULL : N U L L ;
OF : O F ;
OR : O R ;
OUT : O U T ;
PEOPLECODE : P E O P L E C O D E ;
PRIVATE : P R I V A T E ;
PROPERTY : P R O P E R T Y ;
PROTECTED : P R O T E C T E D ;
RBRACKET : ']' ;
READONLY : R E A D O N L Y ;
REF : R E F ;
REPEAT : R E P E A T ;
RETURN : R E T U R N ;
RETURNS : R E T U R N S ;
RPAREN : ')' ;
SEMI : ';' ;
SET : S E T ;
STAR : '*' ;
STEP : S T E P ;
SUBTR : '-' ;
SUPER : '%' S U P E R ;
THEN : T H E N ;
THROW : T H R O W ;
TO : T O ;
TRY : T R Y ;
UNTIL : U N T I L ;
VALUE : V A L U E ;
WARNING : W A R N I N G ;
WHEN : W H E N ;
WHEN_OTHER : W H E N '-' O T H E R ;
WHILE : W H I L E ;
ANY : A N Y ;
BOOLEAN : B O O L E A N ;
DATE : D A T E ;
DATETIME : D A T E T I M E ;
FLOAT : F L O A T ;
NUMBER : N U M B E R ;
STRING : S T R I N G ;
TIME : T I M E ;
PIPE : '|' ;
METADATA : '%' M E T A D A T A ;

DecimalLiteral : IntegerLiteral? '.' [0-9]+ ;
IntegerLiteral : '-'? [0-9]+ ;

StringLiteral
	:	'"' (~'"'|'""')* '"'
	|	'\'' (~'\''|'\'\'')* '\''
	;

BooleanLiteral
	:	T R U E
	|	F A L S E
	;

RecordEvent
	:	F I E L D D E F A U L T
	|	F I E L D E D I T
	|	F I E L D C H A N G E
	|	F I E L D F O R M U L A
	|	R O W I N I T
	|	R O W I N S E R T
	|	R O W D E L E T E
	|	R O W S E L E C T
	|	S A V E E D I T
	|	S A V E P R E C H A N G E
	|	S A V E P O S T C H A N G E
	|	S E A R C H I N I T
	|	S E A R C H S A V E
	|	W O R K F L O W
	|	P R E P O P U P
	;

SYSTEM_VARIABLE : '%' A L L O W N O T I F I C A T I O N
                | '%' A L L O W R E C I P I E N T L O O K U P
                | '%' A P P L I C A T I O N L O G F E N C E
                | '%' A S O F D A T E
                | '%' A U T H E N T I C A T I O N T O K E N
                | '%' B P N A M E
                | '%' C L I E N T D A T E
                | '%' C L I E N T T I M E Z O N E
                | '%' C O M P I N T F C N A M E
                | '%' C O M P O N E N T
                | '%' C O N T E N T I D
                | '%' C O N T E N T T Y P E
                | '%' C O P Y R I G H T
                | '%' C U R R E N C Y
                | '%' D A T E
                | '%' D A T E T I M E
                | '%' D B N A M E
                | '%' D B S E R V E R N A M E
                | '%' D B T Y P E
                | '%' E M A I L A D D R E S S
                | '%' E M P L O Y E E I D
                | '%' E X T E R N A L A U T H I N F O
                | '%' F I L E P A T H
                | '%' H P T A B N A M E
                | '%' I M P O R T
                | '%' I N T B R O K E R
                | '%' I S M U L T I L A N G U A G E E N A B L E D
                | '%' L A N G U A G E
                | '%' L A N G U A G E '_' B A S E
                | '%' L A N G U A G E '_' D A T A
                | '%' L A N G U A G E '_' U S E R
                | '%' L O C A L N O D E
                | '%' M A P '_' M A R K E T
                | '%' M A R K E T
                | '%' M A X M E S S A G E S I Z E
                | '%' M A X N B R S E G M E N T S
                | '%' M E N U
                | '%' M O D E
                | '%' N A V I G A T O R H O M E P E R M I S S I O N L I S T
                | '%' N O D E
                | '%' O P E R A T O R C L A S S
                | '%' O P E R A T O R I D
                | '%' O P E R A T O R R O W L E V E L S E C U R I T Y C L A S S
                | '%' O U T D E S T F O R M A T
                | '%' O U T D E S T T Y P E
                | '%' P A G E
                | '%' P A N E L
                | '%' P A N E L G R O U P
                | '%' P A S S W O R D E X P I R E D
                | '%' P E R F T I M E
                | '%' P E R M I S S I O N L I S T S
                | '%' P I D
                | '%' P O R T A L
                | '%' P R I M A R Y P E R M I S S I O N L I S T
                | '%' P R O C E S S P R O F I L E P E R M I S S I O N L I S T
                | '%' P S A U T H R E S U L T
                | '%' R E Q U E S T
                | '%' R E S P O N S E
                | '%' R E S U L T D O C U M E N T
                | '%' R O L E S
                | '%' R O W S E C U R I T Y P E R M I S S I O N L I S T
                | '%' R U N N I N G I N P O R T A L
                | '%' S E R V E R T I M E Z O N E
                | '%' S E S S I O N
                | '%' S I G N O N U S E R I D
                | '%' S I G N O N U S E R P S W D
                | '%' S M T P B L A C K B E R R Y R E P L Y T O
                | '%' S M T P G U A R A N T E E D
                | '%' S M T P S E N D E R
                | '%' S Q L R O W S
                | '%' T H I S
                | '%' T I M E
                | '%' T R A N S F O R M D A T A
                | '%' U S E R D E S C R I P T I O N
                | '%' U S E R I D
                | '%' W L I N S T A N C E I D
                | '%' W L N A M E
                ;

SYSTEM_CONSTANT : '%' GENERIC_ID_LIMITED ;      // also catches any system variables that may be defined in future versions of PeopleTools
GENERIC_ID_LIMITED : [a-zA-Z_\p{Alpha}\p{General_Category=Other_Letter}] ID_CHAR* ;
GENERIC_ID : ID_1ST_CHAR ID_CHAR* ;
USER_VARIABLE : '&' ID_CHAR+ ;

fragment END : E N D '-' ;
fragment ID_1ST_CHAR : [0-9a-zA-Z_#$\p{Alnum}\p{General_Category=Other_Letter}] ;
fragment ID_CHAR : ID_1ST_CHAR | '@' ;
fragment CRLF : '\r'? '\n' ;    // all line feed characters would be [\r\n\u000B\f\u0085\u2028\u2029]
//fragment HWS  : [ \t\u00A0\u1680\u2000-\u200A\u202F\u205F\u3000] ;      // horizontal whitespace characters

/* case insensitive lexer matching */
fragment A : 'a' | 'A' ;
fragment B : 'b' | 'B' ;
fragment C : 'c' | 'C' ;
fragment D : 'd' | 'D' ;
fragment E : 'e' | 'E' ;
fragment F : 'f' | 'F' ;
fragment G : 'g' | 'G' ;
fragment H : 'h' | 'H' ;
fragment I : 'i' | 'I' ;
fragment J : 'j' | 'J' ;
fragment K : 'k' | 'K' ;
fragment L : 'l' | 'L' ;
fragment M : 'm' | 'M' ;
fragment N : 'n' | 'N' ;
fragment O : 'o' | 'O' ;
fragment P : 'p' | 'P' ;
fragment Q : 'q' | 'Q' ;
fragment R : 'r' | 'R' ;
fragment S : 's' | 'S' ;
fragment T : 't' | 'T' ;
fragment U : 'u' | 'U' ;
fragment V : 'v' | 'V' ;
fragment W : 'w' | 'W' ;
fragment X : 'x' | 'X' ;
fragment Y : 'y' | 'Y' ;
fragment Z : 'z' | 'Z' ;


//******************************************************************************
//* DIRECTIVE PEOPLECODE MODE
//******************************************************************************

mode DIRECTIVE;

DIR_WS : [\p{White_Space}]+ -> channel(WHITESPACE) ;
DIR_THEN : '#' THEN SEMI* -> channel(DIRECTIVES), popMode ;
//DIR_TOOLSREL : '#' T O O L S R E L -> channel(DIRECTIVES) ;
//DIR_AND : '&&' -> channel(DIRECTIVES) ;
//DIR_OR : '||' -> channel(DIRECTIVES) ;
DIR_ATOM : ~[\p{White_Space}]+ -> channel(DIRECTIVES) ;