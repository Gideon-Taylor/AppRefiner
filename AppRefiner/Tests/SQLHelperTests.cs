using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppRefiner.Linters;
using SqlParser.Ast;

namespace AppRefiner.Tests
{
    [TestClass]
    public class SQLHelperTests
    {
        [TestMethod]
        public void ExtractSQLFromLiteral_RemovesQuotes()
        {
            // Arrange
            string literal = "\"SELECT * FROM PS_JOB\"";
            
            // Act
            string result = SQLHelper.ExtractSQLFromLiteral(literal);
            
            // Assert
            Assert.AreEqual("SELECT * FROM PS_JOB", result);
        }
        
        [TestMethod]
        public void ParseSQL_ValidSQL_ReturnsStatement()
        {
            // Arrange
            string sql = "SELECT EMPLID FROM PS_JOB WHERE EMPL_STATUS = :1";
            
            // Act
            var result = SQLHelper.ParseSQL(sql);
            
            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Statement.Select));
        }
        
        [TestMethod]
        public void ParseSQL_InvalidSQL_ReturnsNull()
        {
            // Arrange
            string sql = "SELECT * FROM (invalid syntax";
            
            // Act
            var result = SQLHelper.ParseSQL(sql);
            
            // Assert
            Assert.IsNull(result);
        }
        
        [TestMethod]
        public void GetBindCount_WithBindVariables_ReturnsCorrectCount()
        {
            // Arrange
            string sql = "SELECT EMPLID FROM PS_JOB WHERE EMPL_STATUS = :1 AND DEPTID = :2";
            var statement = SQLHelper.ParseSQL(sql);
            
            // Act
            int bindCount = SQLHelper.GetBindCount(statement);
            
            // Assert
            Assert.AreEqual(2, bindCount);
        }
        
        [TestMethod]
        public void GetOutputCount_ForSelectStatement_ReturnsCorrectCount()
        {
            // Arrange
            string sql = "SELECT EMPLID, NAME, DEPTID FROM PS_JOB";
            var statement = SQLHelper.ParseSQL(sql) as Statement.Select;
            
            // Act
            int outputCount = SQLHelper.GetOutputCount(statement);
            
            // Assert
            Assert.AreEqual(3, outputCount);
        }
        
        [TestMethod]
        public void HasWildcard_WithWildcard_ReturnsTrue()
        {
            // Arrange
            string sql = "SELECT * FROM PS_JOB";
            var statement = SQLHelper.ParseSQL(sql) as Statement.Select;
            
            // Act
            bool hasWildcard = SQLHelper.HasWildcard(statement);
            
            // Assert
            Assert.IsTrue(hasWildcard);
        }
        
        [TestMethod]
        public void HasWildcard_WithoutWildcard_ReturnsFalse()
        {
            // Arrange
            string sql = "SELECT EMPLID, NAME FROM PS_JOB";
            var statement = SQLHelper.ParseSQL(sql) as Statement.Select;
            
            // Act
            bool hasWildcard = SQLHelper.HasWildcard(statement);
            
            // Assert
            Assert.IsFalse(hasWildcard);
        }
    }
}
