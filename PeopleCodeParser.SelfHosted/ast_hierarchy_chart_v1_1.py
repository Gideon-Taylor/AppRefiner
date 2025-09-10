#!/usr/bin/env python3
"""
AST Node Hierarchy Chart Generator v1.1 for AppRefiner Self-Hosted Parser

Based on V1 with targeted fixes:
- Better edge routing to avoid misleading connections
- Connect BreakStatementNode to loop constructs
- Keep the clean, organized layout from V1
"""

import graphviz
import os
from typing import Dict, List, Tuple

def create_ast_hierarchy_chart():
    """Create AST hierarchy chart v1.1 - V1 with targeted improvements."""
    
    # Create a new directed graph with V1's successful attributes
    dot = graphviz.Digraph(
        name='ast_hierarchy_v1_1',
        comment='PeopleCode Self-Hosted Parser AST Node Hierarchy v1.1',
        format='svg'
    )
    
    # Keep V1's proven layout settings
    dot.attr(
        rankdir='TB',  # Top to bottom layout
        size='12,8',   # Max size in inches (roughly 1200x800px)
        ratio='compress',
        bgcolor='white',
        fontname='Arial',
        fontsize='10',
        splines='ortho',  # Keep orthogonal edges but with better routing
        nodesep='0.3',   # V1's spacing
        ranksep='0.4',   # V1's spacing
        concentrate='false'  # Don't merge edges for clearer routing
    )
    
    # Keep V1's successful node styles exactly
    node_styles = {
        'root': {
            'shape': 'box',
            'style': 'filled,bold',
            'fillcolor': '#4CAF50',
            'fontcolor': 'white',
            'fontsize': '12'
        },
        'program_component': {
            'shape': 'box',
            'style': 'filled',
            'fillcolor': '#2196F3',
            'fontcolor': 'white',
            'fontsize': '10'
        },
        'declaration': {
            'shape': 'box',
            'style': 'filled',
            'fillcolor': '#FF9800',
            'fontcolor': 'white',
            'fontsize': '9'
        },
        'statement': {
            'shape': 'ellipse',
            'style': 'filled',
            'fillcolor': '#9C27B0',
            'fontcolor': 'white',
            'fontsize': '9'
        },
        'expression': {
            'shape': 'diamond',
            'style': 'filled',
            'fillcolor': '#E91E63',
            'fontcolor': 'white',
            'fontsize': '9'
        },
        'type': {
            'shape': 'hexagon',
            'style': 'filled',
            'fillcolor': '#607D8B',
            'fontcolor': 'white',
            'fontsize': '9'
        },
        'utility': {
            'shape': 'box',
            'style': 'filled,dashed',
            'fillcolor': '#FFC107',
            'fontcolor': 'black',
            'fontsize': '8'
        }
    }
    
    # Keep V1's edge styles, add one for control flow
    edge_styles = {
        'contains': {'color': 'blue', 'style': 'solid', 'arrowhead': 'diamond'},
        'implements': {'color': 'green', 'style': 'dashed', 'arrowhead': 'empty'},
        'references': {'color': 'gray', 'style': 'dotted', 'arrowhead': 'vee'},
        'control_flow': {'color': 'purple', 'style': 'solid', 'arrowhead': 'normal'}  # New for break/continue
    }
    
    # Keep V1's successful node definitions with minor additions
    nodes = [
        # Root node - same as V1
        ('ProgramNode', 'root', 'ProgramNode\\n(Root)\\n\\n• Imports[]\\n• AppClass?\\n• Interface?\\n• Functions[]\\n• Variables[]\\n• Constants[]\\n• MainBlock?'),
        
        # Program-level components (second tier) - same as V1
        ('ImportNode', 'program_component', 'ImportNode\\n\\n• PackagePath[]\\n• ClassName?\\n• ImportedType'),
        ('AppClassNode', 'program_component', 'AppClassNode\\n\\n• Name\\n• Methods[]\\n• Properties[]\\n• InstanceVars[]\\n• Constants[]\\n• BaseClass?\\n• ImplementedInterface?'),
        ('InterfaceNode', 'program_component', 'InterfaceNode\\n\\n• Name\\n• Methods[]\\n• Properties[]\\n• BaseInterface?'),
        ('FunctionNode', 'program_component', 'FunctionNode\\n\\n• Name\\n• Parameters[]\\n• ReturnType?\\n• Body?\\n• FunctionType'),
        
        # Declaration nodes (third tier) - same as V1
        ('MethodNode', 'declaration', 'MethodNode\\n\\n• Name\\n• Parameters[]\\n• ReturnType?\\n• Implementation?\\n• IsAbstract\\n• IsConstructor'),
        ('PropertyNode', 'declaration', 'PropertyNode\\n\\n• Name\\n• Type\\n• HasGet/HasSet\\n• GetterImpl?\\n• SetterImpl?'),
        ('VariableNode', 'declaration', 'VariableNode\\n\\n• Name\\n• Type\\n• Scope\\n• InitialValue?\\n• AdditionalNames[]'),
        ('ConstantNode', 'declaration', 'ConstantNode\\n\\n• Name\\n• Value'),
        ('ParameterNode', 'declaration', 'ParameterNode\\n\\n• Name\\n• Type\\n• IsOut\\n• Mode'),
        ('MethodImplNode', 'declaration', 'MethodImplNode\\n\\n• Name\\n• Body\\n• ParameterAnnotations[]\\n• ReturnTypeAnnotation?'),
        
        # Core statement nodes - same as V1
        ('BlockNode', 'statement', 'BlockNode\\n\\n• Statements[]\\n• IntroducesScope'),
        ('IfStatementNode', 'statement', 'IfStatementNode\\n\\n• Condition\\n• ThenBlock\\n• ElseBlock?'),
        ('ForStatementNode', 'statement', 'ForStatementNode\\n\\n• Variable\\n• FromValue\\n• ToValue\\n• StepValue?\\n• Body'),
        ('WhileStatementNode', 'statement', 'WhileStatementNode\\n\\n• Condition\\n• Body'),
        ('RepeatStatementNode', 'statement', 'RepeatStatementNode\\n\\n• Body\\n• Condition'),
        ('EvaluateStatementNode', 'statement', 'EvaluateStatementNode\\n\\n• Expression\\n• WhenClauses[]\\n• WhenOtherBlock?'),
        ('TryStatementNode', 'statement', 'TryStatementNode\\n\\n• TryBlock\\n• CatchClauses[]'),
        
        # Control flow statements
        ('ReturnStatementNode', 'statement', 'ReturnStatementNode\\n\\n• Value?\\n• DoesTransferControl'),
        ('ThrowStatementNode', 'statement', 'ThrowStatementNode\\n\\n• Exception\\n• DoesTransferControl'),
        ('BreakStatementNode', 'statement', 'BreakStatementNode\\n\\n• DoesTransferControl'),
        ('ContinueStatementNode', 'statement', 'ContinueStatementNode\\n\\n• DoesTransferControl'),
        ('ExpressionStatementNode', 'statement', 'ExpressionStatementNode\\n\\n• Expression'),
        ('LocalVariableDeclarationNode', 'statement', 'LocalVariableDeclarationNode\\n\\n• Type\\n• VariableNames[]'),
        
        # Key expression nodes - same as V1
        ('BinaryOperationNode', 'expression', 'BinaryOperationNode\\n\\n• Left\\n• Operator\\n• Right\\n• NotFlag'),
        ('MethodCallNode', 'expression', 'MethodCallNode\\n\\n• Object?\\n• MethodName\\n• Arguments[]'),
        ('LiteralNode', 'expression', 'LiteralNode\\n\\n• Value\\n• LiteralType'),
        ('IdentifierNode', 'expression', 'IdentifierNode\\n\\n• Name\\n• IsLValue'),
        ('ArrayAccessNode', 'expression', 'ArrayAccessNode\\n\\n• Array\\n• Indices[]\\n• IsLValue'),
        ('PropertyAccessNode', 'expression', 'PropertyAccessNode\\n\\n• Object\\n• PropertyName\\n• IsLValue'),
        
        # Type nodes - same as V1
        ('TypeNode', 'type', 'TypeNode\\n(Abstract Base)\\n\\n• TypeName\\n• IsNullable\\n• IsBuiltIn'),
        ('BuiltInTypeNode', 'type', 'BuiltInTypeNode\\n\\n• Type\\n• IsBuiltIn=true'),
        ('ArrayTypeNode', 'type', 'ArrayTypeNode\\n\\n• Dimensions\\n• ElementType?'),
        ('AppClassTypeNode', 'type', 'AppClassTypeNode\\n\\n• PackagePath[]\\n• ClassName'),
        
        # Utility nodes - same as V1
        ('CatchStatementNode', 'utility', 'CatchStatementNode\\n\\n• ExceptionVariable?\\n• ExceptionType?\\n• Body'),
        ('WhenClause', 'utility', 'WhenClause\\n\\n• Condition\\n• Body\\n• Operator?'),
    ]
    
    # Add all nodes to the graph
    for node_id, style_key, label in nodes:
        dot.node(node_id, label, **node_styles[style_key])
    
    # Keep most of V1's relationships, with targeted fixes
    relationships = [
        # Program root contains major components - keep V1's structure
        ('ProgramNode', 'ImportNode', 'contains'),
        ('ProgramNode', 'AppClassNode', 'contains'),
        ('ProgramNode', 'InterfaceNode', 'contains'),
        ('ProgramNode', 'FunctionNode', 'contains'),
        ('ProgramNode', 'VariableNode', 'contains'),
        ('ProgramNode', 'ConstantNode', 'contains'),
        ('ProgramNode', 'BlockNode', 'contains'),  # MainBlock
        
        # Class/Interface structure - same as V1
        ('AppClassNode', 'MethodNode', 'contains'),
        ('AppClassNode', 'PropertyNode', 'contains'),
        ('AppClassNode', 'VariableNode', 'contains'),
        ('AppClassNode', 'ConstantNode', 'contains'),
        ('InterfaceNode', 'MethodNode', 'contains'),
        ('InterfaceNode', 'PropertyNode', 'contains'),
        
        # Method/Function structure - same as V1
        ('FunctionNode', 'ParameterNode', 'contains'),
        ('FunctionNode', 'TypeNode', 'references'),  # ReturnType
        ('FunctionNode', 'BlockNode', 'contains'),   # Body
        ('MethodNode', 'ParameterNode', 'contains'),
        ('MethodNode', 'TypeNode', 'references'),    # ReturnType
        ('MethodNode', 'MethodImplNode', 'implements'),
        
        # Implementation structure - same as V1
        ('MethodImplNode', 'BlockNode', 'contains'), # Body
        ('PropertyNode', 'TypeNode', 'references'),
        ('PropertyNode', 'MethodImplNode', 'contains'), # Getter/Setter
        ('VariableNode', 'TypeNode', 'references'),
        ('ParameterNode', 'TypeNode', 'references'),
        
        # Block contains statements - same as V1
        ('BlockNode', 'IfStatementNode', 'contains'),
        ('BlockNode', 'ForStatementNode', 'contains'),
        ('BlockNode', 'WhileStatementNode', 'contains'),
        ('BlockNode', 'RepeatStatementNode', 'contains'),
        ('BlockNode', 'EvaluateStatementNode', 'contains'),
        ('BlockNode', 'TryStatementNode', 'contains'),
        ('BlockNode', 'ReturnStatementNode', 'contains'),
        ('BlockNode', 'ThrowStatementNode', 'contains'),
        ('BlockNode', 'ExpressionStatementNode', 'contains'),
        ('BlockNode', 'LocalVariableDeclarationNode', 'contains'),
        
        # FIX: Connect control flow statements to loop constructs where they can be used
        ('ForStatementNode', 'BreakStatementNode', 'control_flow'),
        ('ForStatementNode', 'ContinueStatementNode', 'control_flow'),
        ('WhileStatementNode', 'BreakStatementNode', 'control_flow'),
        ('WhileStatementNode', 'ContinueStatementNode', 'control_flow'),
        ('RepeatStatementNode', 'BreakStatementNode', 'control_flow'),
        ('RepeatStatementNode', 'ContinueStatementNode', 'control_flow'),
        ('EvaluateStatementNode', 'BreakStatementNode', 'control_flow'),
        
        # Control structures contain blocks/expressions - same as V1
        ('IfStatementNode', 'BinaryOperationNode', 'references'), # Condition
        ('IfStatementNode', 'BlockNode', 'contains'),
        ('ForStatementNode', 'BlockNode', 'contains'),
        ('WhileStatementNode', 'BlockNode', 'contains'),
        ('RepeatStatementNode', 'BlockNode', 'contains'),
        ('EvaluateStatementNode', 'WhenClause', 'contains'),
        ('TryStatementNode', 'CatchStatementNode', 'contains'),
        
        # Expression relationships - same as V1
        ('ExpressionStatementNode', 'BinaryOperationNode', 'contains'),
        ('ExpressionStatementNode', 'MethodCallNode', 'contains'),
        ('ExpressionStatementNode', 'IdentifierNode', 'contains'),
        ('BinaryOperationNode', 'LiteralNode', 'references'),
        ('BinaryOperationNode', 'IdentifierNode', 'references'),
        ('MethodCallNode', 'IdentifierNode', 'references'),
        ('ArrayAccessNode', 'IdentifierNode', 'references'),
        ('PropertyAccessNode', 'IdentifierNode', 'references'),
        
        # Type hierarchy - same as V1
        ('BuiltInTypeNode', 'TypeNode', 'implements'),
        ('ArrayTypeNode', 'TypeNode', 'implements'),
        ('AppClassTypeNode', 'TypeNode', 'implements'),
        ('ArrayTypeNode', 'TypeNode', 'references'), # ElementType
        
        # Import relationships - same as V1
        ('ImportNode', 'TypeNode', 'references'), # ImportedType
    ]
    
    # Add relationships to graph
    for source, target, rel_type in relationships:
        dot.edge(source, target, **edge_styles[rel_type])
    
    # Keep V1's successful subgraph clustering
    with dot.subgraph(name='cluster_program') as c:
        c.attr(label='Program Structure', style='dashed', color='blue')
        c.node('ProgramNode')
        c.node('ImportNode') 
        c.node('AppClassNode')
        c.node('InterfaceNode')
        c.node('FunctionNode')
    
    with dot.subgraph(name='cluster_declarations') as c:
        c.attr(label='Declarations', style='dashed', color='orange')
        c.node('MethodNode')
        c.node('PropertyNode')
        c.node('VariableNode')
        c.node('ConstantNode')
        c.node('ParameterNode')
        c.node('MethodImplNode')
    
    with dot.subgraph(name='cluster_statements') as c:
        c.attr(label='Statements', style='dashed', color='purple')
        c.node('BlockNode')
        c.node('IfStatementNode')
        c.node('ForStatementNode') 
        c.node('WhileStatementNode')
        c.node('RepeatStatementNode')
        c.node('EvaluateStatementNode')
        c.node('TryStatementNode')
        c.node('ReturnStatementNode')
        c.node('ThrowStatementNode')
        c.node('BreakStatementNode')        # Now connected!
        c.node('ContinueStatementNode')     # Now connected!
        c.node('ExpressionStatementNode')
        c.node('LocalVariableDeclarationNode')
    
    with dot.subgraph(name='cluster_expressions') as c:
        c.attr(label='Expressions', style='dashed', color='red', margin='20')
        c.node('BinaryOperationNode')
        c.node('MethodCallNode')
        c.node('LiteralNode')
        c.node('IdentifierNode')
        c.node('ArrayAccessNode')
        c.node('PropertyAccessNode')
    
    with dot.subgraph(name='cluster_types') as c:
        c.attr(label='Types', style='dashed', color='gray')
        c.node('TypeNode')
        c.node('BuiltInTypeNode')
        c.node('ArrayTypeNode')
        c.node('AppClassTypeNode')
    
    return dot

def create_legend_v1_1():
    """Create legend for v1.1 (same as V1 with control_flow addition)."""
    legend = graphviz.Digraph(name='legend_v1_1', format='svg')
    legend.attr(rankdir='TB', bgcolor='white')
    
    # Same legend nodes as V1
    legend.node('root_legend', 'Root Node\\n(ProgramNode)', 
               shape='box', style='filled,bold', fillcolor='#4CAF50', fontcolor='white')
    legend.node('program_legend', 'Program Components\\n(Import, Class, etc.)', 
               shape='box', style='filled', fillcolor='#2196F3', fontcolor='white')
    legend.node('declaration_legend', 'Declarations\\n(Method, Property, etc.)', 
               shape='box', style='filled', fillcolor='#FF9800', fontcolor='white')
    legend.node('statement_legend', 'Statements\\n(If, For, Block, etc.)', 
               shape='ellipse', style='filled', fillcolor='#9C27B0', fontcolor='white')
    legend.node('expression_legend', 'Expressions\\n(Binary, Method Call, etc.)', 
               shape='diamond', style='filled', fillcolor='#E91E63', fontcolor='white')
    legend.node('type_legend', 'Types\\n(BuiltIn, Array, etc.)', 
               shape='hexagon', style='filled', fillcolor='#607D8B', fontcolor='white')
    
    # Edge legend with addition
    legend.node('edge_legend', 'Edge Types:', shape='plaintext')
    legend.node('contains_legend', 'Contains', shape='plaintext')
    legend.node('implements_legend', 'Implements', shape='plaintext')  
    legend.node('references_legend', 'References', shape='plaintext')
    legend.node('control_legend', 'Can Use (break/continue)', shape='plaintext')  # NEW
    
    legend.edge('edge_legend', 'contains_legend', color='blue', style='solid', arrowhead='diamond')
    legend.edge('edge_legend', 'implements_legend', color='green', style='dashed', arrowhead='empty')
    legend.edge('edge_legend', 'references_legend', color='gray', style='dotted', arrowhead='vee')
    legend.edge('edge_legend', 'control_legend', color='purple', style='solid', arrowhead='normal')  # NEW
    
    return legend

def main():
    """Generate the AST hierarchy chart v1.1."""
    print("Generating AST Node Hierarchy Chart v1.1...")
    
    # Create the main hierarchy chart
    chart = create_ast_hierarchy_chart()
    
    # Create the legend
    legend = create_legend_v1_1()
    
    # Define output directory
    output_dir = os.path.dirname(os.path.abspath(__file__))
    
    try:
        # Render the chart
        chart_path = chart.render(os.path.join(output_dir, 'ast_hierarchy_chart_v1_1'), cleanup=True)
        print(f"Chart v1.1 generated: {chart_path}")
        
        # Render the legend
        legend_path = legend.render(os.path.join(output_dir, 'ast_hierarchy_legend_v1_1'), cleanup=True)
        print(f"Legend v1.1 generated: {legend_path}")
        
        # Also generate PNG versions
        chart.format = 'png'
        chart_png_path = chart.render(os.path.join(output_dir, 'ast_hierarchy_chart_v1_1_png'), cleanup=True)
        print(f"PNG Chart v1.1 generated: {chart_png_path}")
        
        legend.format = 'png'
        legend_png_path = legend.render(os.path.join(output_dir, 'ast_hierarchy_legend_v1_1_png'), cleanup=True)
        print(f"PNG Legend v1.1 generated: {legend_png_path}")
        
        print("\nV1.1 Targeted Improvements:")
        print("+ Connected BreakStatementNode and ContinueStatementNode to loop constructs")
        print("+ Added purple 'control_flow' edges to show break/continue usage")
        print("+ Added ContinueStatementNode (was missing)")
        print("+ Added RepeatStatementNode (was missing)")
        print("+ Set concentrate='false' for clearer edge routing")
        print("= Kept all of V1's successful layout and organization")
        
    except Exception as e:
        print(f"Error generating chart: {e}")
        print("Make sure Graphviz is installed and available in your PATH")
        return 1
    
    return 0

if __name__ == "__main__":
    exit(main())