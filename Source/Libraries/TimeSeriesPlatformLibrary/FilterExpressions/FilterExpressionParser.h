//******************************************************************************************************
//  FilterExpressionParser.h - Gbtc
//
//  Copyright � 2018, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  11/01/2018 - J. Ritchie Carroll
//       Generated original version of source code.
//
//******************************************************************************************************

#ifndef __FILTER_EXPRESSION_PARSER_H
#define __FILTER_EXPRESSION_PARSER_H

#include "FilterExpressions.h"
#include "FilterExpressionSyntaxLexer.h"
#include "ExpressionTree.h"

#ifdef _DEBUG
#define SUPPRESS_CONSOLE_ERROR_OUTPUT false
#else
#define SUPPRESS_CONSOLE_ERROR_OUTPUT true
#endif

namespace GSF {
namespace FilterExpressions
{

// Simple exception type thrown by the filter expression parser
class FilterExpressionParserException : public Exception
{
private:
    std::string m_message;

public:
    FilterExpressionParserException(std::string message) noexcept;
    const char* what() const noexcept;
};

struct TableIDFields
{
    std::string SignalIDFieldName;
    std::string MeasurementKeyFieldName;
    std::string PointTagFieldName;
};

typedef GSF::SharedPtr<TableIDFields> TableIDFieldsPtr;

class FilterExpressionParser;
typedef GSF::SharedPtr<FilterExpressionParser> FilterExpressionParserPtr;

class FilterExpressionParser : // NOLINT
    public FilterExpressionSyntaxBaseListener,
    public GSF::EnableSharedThisPtr<FilterExpressionParser>
{
private:
    typedef void(*ParsingExceptionCallback)(FilterExpressionParserPtr, const std::string&);

    class CallbackErrorListener : public antlr4::BaseErrorListener
    {
    private:
        const FilterExpressionParserPtr m_filterExpressionParser;
        const ParsingExceptionCallback m_parsingExceptionCallback;

    public:
        CallbackErrorListener(FilterExpressionParserPtr filterExpressionParser, ParsingExceptionCallback parsingExceptionCallback);
        void syntaxError(antlr4::Recognizer* recognizer, antlr4::Token* offendingSymbol, size_t line, size_t charPositionInLine, const std::string& msg, std::exception_ptr e) override;
    };

    antlr4::ANTLRInputStream m_inputStream;
    FilterExpressionSyntaxLexer* m_lexer;
    antlr4::CommonTokenStream* m_tokens;
    FilterExpressionSyntaxParser* m_parser;
    CallbackErrorListener* m_callbackErrorListener;
    GSF::Data::DataSetPtr m_dataSet;
    ExpressionTreePtr m_activeExpressionTree;
    bool m_trackFilteredSignalIDs;
    bool m_trackFilteredRows;

    std::string m_primaryTableName;
    std::unordered_set<GSF::Guid> m_filteredSignalIDSet;
    std::vector<GSF::Guid> m_filteredSignalIDs;
    std::vector<GSF::Data::DataRowPtr> m_filteredRows;
    std::vector<ExpressionTreePtr> m_expressionTrees;
    std::map<const antlr4::ParserRuleContext*, ExpressionPtr> m_expressions;
    std::map<const std::string, TableIDFieldsPtr> m_tableIDFields;

    bool TryGetExpr(const antlr4::ParserRuleContext* context, ExpressionPtr& expression) const;
    void AddExpr(const antlr4::ParserRuleContext* context, const ExpressionPtr& expression);
    void MapMeasurement(const GSF::Data::DataTablePtr& measurements, int32_t signalIDColumnIndex, const std::string& columnName, const std::string& mappingValue);
    void VisitParseTreeNodes();
public:
    FilterExpressionParser(const std::string& filterExpression, bool suppressConsoleErrorOutput = SUPPRESS_CONSOLE_ERROR_OUTPUT);
    ~FilterExpressionParser();

    const GSF::Data::DataSetPtr& GetDataSet() const;
    void SetDataSet(const GSF::Data::DataSetPtr& dataSet);

    TableIDFieldsPtr GetTableIDFields(const std::string& tableName) const;
    void SetTableIDFields(const std::string& tableName, const TableIDFieldsPtr& tableIDFields);

    const std::string& GetPrimaryTableName() const;
    void SetPrimaryTableName(const std::string& tableName);

    // ParsingExceptionCallback function is defined with the following signature:
    //   void HandleParsingException(FilterExpressionParserPtr, const string& message)
    void RegisterParsingExceptionCallback(ParsingExceptionCallback parsingExceptionCallback);

    void Evaluate();

    bool GetTrackFilteredSignalIDs() const;
    void SetTrackFilteredSignalIDs(bool trackFilteredSignalIDs);
    const std::vector<GSF::Guid>& FilteredSignalIDs() const;
    const std::unordered_set<GSF::Guid>& FilteredSignalIDSet() const;
    
    bool GetTrackFilteredRows() const;
    void SetTrackFilteredRows(bool trackFilteredRows);
    const std::vector<GSF::Data::DataRowPtr>& FilteredRows() const;

    const std::vector<ExpressionTreePtr>& GetExpressionTrees();

    void enterFilterExpressionStatement(FilterExpressionSyntaxParser::FilterExpressionStatementContext*) override;
    void enterFilterStatement(FilterExpressionSyntaxParser::FilterStatementContext*) override;
    void exitIdentifierStatement(FilterExpressionSyntaxParser::IdentifierStatementContext*) override;
    void enterExpression(FilterExpressionSyntaxParser::ExpressionContext*) override;
    void exitExpression(FilterExpressionSyntaxParser::ExpressionContext*) override;
    void exitPredicateExpression(FilterExpressionSyntaxParser::PredicateExpressionContext*) override;
    void exitValueExpression(FilterExpressionSyntaxParser::ValueExpressionContext*) override;
    void exitLiteralValue(FilterExpressionSyntaxParser::LiteralValueContext*) override;
    void exitColumnName(FilterExpressionSyntaxParser::ColumnNameContext*) override;
    void exitFunctionExpression(FilterExpressionSyntaxParser::FunctionExpressionContext*) override;

    static std::vector<ExpressionTreePtr> GenerateExpressionTrees(const GSF::Data::DataTablePtr& dataTable, const std::string& filterExpression, bool suppressConsoleErrorOutput = SUPPRESS_CONSOLE_ERROR_OUTPUT);
    static ExpressionTreePtr GenerateExpressionTree(const GSF::Data::DataTablePtr& dataTable, const std::string& filterExpression, bool suppressConsoleErrorOutput = SUPPRESS_CONSOLE_ERROR_OUTPUT);
    static ValueExpressionPtr Evaluate(const GSF::Data::DataRowPtr& dataRow, const std::string& filterExpression, bool suppressConsoleErrorOutput = SUPPRESS_CONSOLE_ERROR_OUTPUT);
    static std::vector<GSF::Data::DataRowPtr> Select(const GSF::Data::DataTablePtr& dataTable, const std::string& filterExpression, bool suppressConsoleErrorOutput = SUPPRESS_CONSOLE_ERROR_OUTPUT);
};

typedef GSF::SharedPtr<FilterExpressionParser> FilterExpressionParserPtr;

}}

#endif