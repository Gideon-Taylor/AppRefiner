//******************************************************************************
//* ANTLR 4 PARSER GRAMMAR FOR PEOPLECODE PROGRAMS AND APPLICATION CLASSES
//* by Leandro Baca
//******************************************************************************

parser grammar PeopleCodeParser;

options {
	tokenVocab = PeopleCodeLexer;
}


//******************************************************************************
//* MAIN ENTRY POINTS FOR PARSER
//******************************************************************************

/**
 * Entry point for a PeopleCode program.
 */
program
	:   appClass
	|	importsBlock programPreambles? SEMI* statements? SEMI* EOF
	;


//******************************************************************************
//* ADDITIONAL PARSER RULES
//******************************************************************************

importsBlock
	:	importDeclaration*
	;

importDeclaration
	:	IMPORT (appPackageAll | appClassPath) SEMI+
	;

appClass
	:	importsBlock classDeclaration (SEMI+ classExternalDeclaration)* (SEMI* classBody)? SEMI* EOF		#AppClassProgram
	|	importsBlock interfaceDeclaration SEMI* EOF														#InterfaceProgram
	;

appPackageAll
	:	appPackagePath COLON STAR
	;

appPackagePath
	:	(METADATA | genericID) (COLON genericID (COLON genericID)?)?
	;

appClassPath
	:	appPackagePath COLON genericID
	;

classDeclaration
	:	CLASS genericID EXTENDS superclass SEMI* classHeader END_CLASS			#ClassDeclarationExtension
	|	CLASS genericID IMPLEMENTS appClassPath SEMI* classHeader END_CLASS		#ClassDeclarationImplementation
	|	CLASS genericID SEMI* classHeader END_CLASS								#ClassDeclarationPlain
	;

interfaceDeclaration
	:	INTERFACE genericID EXTENDS superclass SEMI* classHeader END_INTERFACE	#InterfaceDeclarationExtension
	|	INTERFACE genericID SEMI* classHeader END_INTERFACE						#InterfaceDeclarationPlain
	;

superclass
	:	EXCEPTION		#ExceptionSuperClass
	|	appClassPath	#AppClassSuperClass
	|	simpleType		#SimpleTypeSuperclass
	;

classHeader
	:	publicHeader? (PROTECTED SEMI* protectedHeader?)? (PRIVATE SEMI* privateHeader?)?
	;

nonPrivateHeader
	:	nonPrivateMember (SEMI+ nonPrivateMember)* SEMI*
	;

publicHeader
	:	nonPrivateHeader
	;

protectedHeader
	:	nonPrivateHeader
	;

privateHeader
	:	privateMember (SEMI+ privateMember)* SEMI*
	;

nonPrivateMember
	:	methodHeader			#NonPrivateMethodHeader
	|	propertyDeclaration		#NonPrivateProperty
	;

privateMember
	:	methodHeader			#PrivateMethodHeader
	|	instanceDeclaration		#PrivateProperty
	|	constantDeclaration		#PrivateConstant
	;

// In parser listener/visitor, make sure constructor returns nothing
methodHeader
	:	METHOD genericID LPAREN methodArguments? RPAREN (RETURNS typeT)? ABSTRACT?
	;

methodArguments
	:	methodArgument (COMMA methodArgument)* COMMA?	// trailing comma is allowed
	;

methodArgument
	:	USER_VARIABLE AS typeT OUT?
	;

simpleType
	:	builtInType			#SimpleBuiltInType
	|	GENERIC_ID_LIMITED	#SimpleGenericID
	;

builtInType
	:	ANY
	|	BOOLEAN
	|	DATE
	|	DATETIME
	|	FLOAT
	|	INTEGER
	|	NUMBER
	|	STRING
	|	TIME
	;

typeT
	:	ARRAY (OF ARRAY)* (OF typeT)?	#ArrayType		// the last "of" is optional if no base type is specified, in which case "any" should be presumed
	|	EXCEPTION						#BaseExceptionType
	|	appClassPath					#AppClassType
	|	simpleType						#SimpleTypeType
	;

propertyDeclaration
	:	PROPERTY typeT genericID GET SET?				#PropertyGetSet
	|	PROPERTY typeT genericID ABSTRACT? READONLY?	#PropertyDirect	// abstract is sometimes featured before readonly in some delivered classes
	;

instanceDeclaration
	:	INSTANCE typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?	#InstanceDecl	// trailing comma is allowed
	|	INSTANCE typeT												#EmptyInstanceDecl	// compiles yet is meaningless
	;

constantDeclaration
	:	CONSTANT USER_VARIABLE EQ literal
	;

literal
	:	NULL
	|	DecimalLiteral
	|	IntegerLiteral
	|	StringLiteral
	|	BooleanLiteral
	;

classExternalDeclaration
	:	functionDeclaration
	|	nonLocalVarDeclaration
	;

programPreambles
	:	programPreamble (SEMI+ programPreamble)*
	;

programPreamble
	:	functionDeclaration
	|	nonLocalVarDeclaration
	|	constantDeclaration
	|	localVariableDefinition
	|	functionDefinition
	;

functionDeclaration
	:	functionDeclarationPCode	#PeopleCodeFunctionDeclaration
	|	functionDeclarationDLL		#LibraryFunctionDeclaration
	;

functionDeclarationPCode
	:	DECLARE FUNCTION genericID PEOPLECODE recordField RecordEvent
	;

recordField
	:	genericID DOT genericID
	;

functionDeclarationDLL
	:	DECLARE FUNCTION genericID LIBRARY StringLiteral (ALIAS StringLiteral)? dllArguments? (RETURNS dllReturnType)?
	;

dllArguments
	:	LPAREN dllArgument (COMMA dllArgument)* RPAREN
	;

dllArgument
	:	genericID (REF | VALUE)? (AS builtInType)?
	;

dllReturnType
	:	genericID AS builtInType
	|	builtInType
	;

nonLocalVarDeclaration
	:	(COMPONENT | GLOBAL) typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?	// trailing comma is allowed
	|	(COMPONENT | GLOBAL) typeT	// compiles yet is meaningless
	;

classBody
	:	classMember (SEMI+ classMember)*
	;

classMember
	:	method		#MethodImplementation
	|	getter		#GetterImplementation
	|	setter		#SetterImplementation
	;

method
	:	METHOD genericID SEMI* methodAnnotations statements? END_METHOD
	;

getter
	:	GET genericID methodReturnAnnotation SEMI* statements END_GET
	;

setter
	:	SET genericID methodParameterAnnotation SEMI* statements? END_SET
	;

statements
	:	statement (SEMI+ statement)* SEMI*
	;

// statementBlock differs from statements only in that it signals a new scope for program symbols
statementBlock
	:	statements
	;

statement
	:	SUPER EQ expression			#SuperAssignmentStmt
	|	localVariableDeclaration	#LocalVarDeclarationStmt
	|	ifStatement					#IfStmt
	|	forStatement				#ForStmt
	|	whileStatement				#WhileStmt
	|	repeatStatement				#RepeatStmt
	|	evaluateStatement			#EvaluateStmt
	|	tryCatchBlock				#TryCatchBlockStmt
	|	EXIT expression?			#ExitStmt
	|	BREAK						#BreakStmt
	|	CONTINUE					#ContinueStmt
	|	ERROR expression			#ErrorStmt
	|	WARNING expression			#WarningStmt
	|	RETURN expression?			#ReturnStmt
	|	THROW expression			#ThrowStmt
//	|	expression EQ expression	#AssignmentStmt	// as it stands, this rule interferes with simpleFunctionCall being recognized as a statement
	|	expression					#ExpressionStmt	// for simpleFunctionCall only, but if omitted, assignment statement never gets parsed (always assumed to be equality expression)
	;

localVariableDeclaration
	:	localVariableDefinition
	|	localVariableDeclAssignment
	;

localVariableDefinition
	:	LOCAL typeT USER_VARIABLE (COMMA USER_VARIABLE)* COMMA?	// trailing comma is allowed
	;

localVariableDeclAssignment
	:	LOCAL typeT USER_VARIABLE EQ expression
	;

ifStatement
	:	IF expression THEN SEMI* statementBlock? (ELSE SEMI* statementBlock?)? END_IF
	;

forStatement
	:	FOR USER_VARIABLE EQ expression TO expression (STEP expression)? SEMI* statementBlock? END_FOR
	;

whileStatement
	:	WHILE expression SEMI* statementBlock? END_WHILE
	;

repeatStatement
	:	REPEAT SEMI* statementBlock? UNTIL expression
	;

evaluateStatement
	:	EVALUATE expression SEMI* whenClauses? whenOther? END_EVALUATE
	;

whenClauses
	:	whenClause (SEMI* whenClause)*
	;

whenClause
	:	WHEN comparisonOperator? expression SEMI* statementBlock?
	;

whenOther
	:	WHEN_OTHER SEMI* statementBlock?
	;

comparisonOperator
	:	LE
	|	GE
	|	NEQ
	|	LT
	|	GT
	|	EQ
	;

tryCatchBlock
	:	TRY SEMI* statementBlock? catchClauses? SEMI* END_TRY
	;

catchClauses
	:	catchClause (SEMI* catchClause)*
	;

catchClause
	:	CATCH (EXCEPTION | appClassPath) USER_VARIABLE SEMI* statementBlock?
	;

expression
	:	LPAREN expression RPAREN							#ParenthesizedExpr
	|	AT expression										#AtExpr
	|	objectCreate										#ObjectCreateExpr
	|	expression AS (appClassPath | genericID)			#ClassCastExpr
	|	expression LBRACKET expressionList RBRACKET			#ArrayIndexExpr
	|	simpleFunctionCall									#FunctionCallExpr
	|	expression dotAccess+								#DotAccessExpr
	|	expression DOT StringLiteral						#StringObjectReferenceExpr
	|	expression LPAREN expression RPAREN					#ImplicitSubindexExpr
	|	SUBTR expression									#NegationExpr
	|	<assoc=right> expression EXP expression				#ExponentialExpr
	|	expression op=(STAR | DIV) expression				#MultDivExpr
	|	expression op=(ADD | SUBTR) expression				#AddSubtrExpr
	|	NOT expression										#NotExpr
	|	expression NOT? op=(LE | GE | LT | GT) expression	#ComparisonExpr
	|	expression NOT? op=(NEQ | EQ) expression			#EqualityExpr
	|	expression op=(AND | OR) expression					#AndOrExpr
	|	expression PIPE expression							#ConcatenationExpr
	|	literal												#LiteralExpr
	|	ident												#IdentifierExpr
	|	appClassPath										#MetadataExpr
	;

simpleFunctionCall
	:	genericID LPAREN functionCallArguments? RPAREN
	;

dotAccess
	:	DOT genericID (LPAREN functionCallArguments? RPAREN)?
	;

allowableFunctionName
	:	ANY
	|	ARRAY
	|	BOOLEAN
	|	COMPONENT
	|	CONSTANT
	|	DATETIME
	|	DOC
	|	EXCEPTION
	|	FLOAT
	|	NUMBER
	|	OF
	|	STEP
	|	RecordEvent
	|	GENERIC_ID
	|	GENERIC_ID_LIMITED
	;

genericID
	:	CATCH
	|	CLASS
	|	CONTINUE
	|	CREATE
	|	DATE
	|	EXTENDS
	|	GET
	|	IMPORT
	|	INSTANCE
	|	INTEGER
	|	INTERFACE
	|	METHOD
	|	OUT
	|	PRIVATE
	|	PROPERTY
	|	READONLY
	|	SET
	|	STRING
	|	THROW
	|	TIME
	|	TRY
	|	VALUE
	|	allowableFunctionName
	;

ident
	:	SUPER				#IdentSuper
	|	SYSTEM_VARIABLE		#IdentSystemVariable
	|	SYSTEM_CONSTANT		#IdentSystemConstant
	|	USER_VARIABLE		#IdentUserVariable
	|	genericID			#IdentGenericID
	;

expressionList
	:	expression (COMMA expression)*
	;

objectCreate
	:	CREATE appClassPath LPAREN functionCallArguments? RPAREN
	;

functionCallArguments
	:	expression (COMMA expression)*
	;

functionDefinitions
	:	functionDefinition (SEMI+ functionDefinition)*
	;

functionDefinition
	:	FUNCTION allowableFunctionName (LPAREN functionArguments? RPAREN)? (RETURNS typeT)? (DOC StringLiteral)? SEMI* statements? END_FUNCTION
	;

functionArguments
	:	functionArgument (COMMA functionArgument)* COMMA?	// trailing comma is allowed
	;

functionArgument
	:	USER_VARIABLE (AS typeT)?
	;

methodAnnotations
	:	methodParameterAnnotation* methodReturnAnnotation? methodExtendsAnnotation?
	;

methodParameterAnnotation
	:	SLASH_PLUS methodArgument COMMA? PLUS_SLASH
	;

methodReturnAnnotation
	:	SLASH_PLUS RETURNS typeT PLUS_SLASH
	;

methodExtendsAnnotation
    :   SLASH_PLUS EXTENDS DIV IMPLEMENTS appClassPath DOT genericID PLUS_SLASH
	;