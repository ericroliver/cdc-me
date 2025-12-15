using System;
using System.Threading.Tasks;
using Xunit;
using Softbase.Cdc.Utilities;

namespace Softbase.Cdc.Tests.Utilities
{
    /// <summary>
    /// Tests for SQL identifier validation to prevent SQL injection attacks.
    /// </summary>
    public class SqlIdentifierValidatorTests
    {
        /// <summary>
        /// Tests that valid identifiers are accepted.
        /// </summary>
        /// <param name="identifier">The identifier to validate.</param>
        [Theory]
        [InlineData("MyTable")]
        [InlineData("my_table")]
        [InlineData("Table123")]
        [InlineData("_table")]
        [InlineData("@tempTable")]
        [InlineData("#tempTable")]
        [InlineData("Table_With_Underscores")]
        [InlineData("a")]
        [InlineData("A1B2C3")]
        public void ValidateIdentifier_ValidIdentifiers_ReturnsIdentifier(string identifier)
        {
            // Act
            var result = SqlIdentifierValidator.ValidateIdentifier(identifier, "test");

            // Assert
            Assert.Equal(identifier, result);
        }

        /// <summary>
        /// Tests that identifiers with brackets are properly handled.
        /// </summary>
        /// <param name="identifier">The identifier with brackets.</param>
        /// <param name="expected">The expected cleaned identifier.</param>
        [Theory]
        [InlineData("[MyTable]", "MyTable")]
        [InlineData("[my_table]", "my_table")]
        [InlineData("[Table123]", "Table123")]
        public void ValidateIdentifier_IdentifiersWithBrackets_ReturnsCleaned(string identifier, string expected)
        {
            // Act
            var result = SqlIdentifierValidator.ValidateIdentifier(identifier, "test");

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that invalid identifiers are rejected.
        /// </summary>
        /// <param name="identifier">The invalid identifier.</param>
        [Theory]
        [InlineData("Table;DROP TABLE users--")]
        [InlineData("Table' OR '1'='1")]
        [InlineData("Table--comment")]
        [InlineData("Table/*comment*/")]
        [InlineData("Table;")]
        [InlineData("Table'")]
        [InlineData("Table\"")]
        [InlineData("Table`")]
        [InlineData("Table\n")]
        [InlineData("Table\r")]
        [InlineData("Table\t")]
        [InlineData("Table ")]
        [InlineData(" Table")]
        [InlineData("123Table")] // Cannot start with digit
        [InlineData("$Table")] // Cannot start with $
        public void ValidateIdentifier_InvalidIdentifiers_ThrowsArgumentException(string identifier)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(identifier, "test"));
            
            Assert.Contains("invalid characters", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests that null or empty identifiers are rejected.
        /// </summary>
        /// <param name="identifier">The null or empty identifier.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidateIdentifier_NullOrEmpty_ThrowsArgumentException(string identifier)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(identifier, "test"));
            
            Assert.Contains("cannot be null or empty", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests that reserved keywords are rejected.
        /// </summary>
        /// <param name="keyword">The reserved keyword.</param>
        [Theory]
        [InlineData("SELECT")]
        [InlineData("select")]
        [InlineData("INSERT")]
        [InlineData("UPDATE")]
        [InlineData("DELETE")]
        [InlineData("DROP")]
        [InlineData("CREATE")]
        [InlineData("ALTER")]
        [InlineData("EXEC")]
        [InlineData("EXECUTE")]
        [InlineData("MASTER")]
        [InlineData("TEMPDB")]
        [InlineData("SYS")]
        public void ValidateIdentifier_ReservedKeywords_ThrowsArgumentException(string keyword)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(keyword, "test"));
            
            Assert.Contains("reserved SQL Server keyword", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests that identifiers exceeding maximum length are rejected.
        /// </summary>
        [Fact]
        public void ValidateIdentifier_TooLong_ThrowsArgumentException()
        {
            // Arrange - Create identifier longer than 128 characters
            var longIdentifier = new string('a', 129);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(longIdentifier, "test"));
            
            Assert.Contains("invalid characters", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests that identifiers at maximum length are accepted.
        /// </summary>
        [Fact]
        public void ValidateIdentifier_MaxLength_ReturnsIdentifier()
        {
            // Arrange - Create identifier exactly 128 characters
            var maxLengthIdentifier = new string('a', 128);

            // Act
            var result = SqlIdentifierValidator.ValidateIdentifier(maxLengthIdentifier, "test");

            // Assert
            Assert.Equal(maxLengthIdentifier, result);
        }

        /// <summary>
        /// Tests that EscapeIdentifier properly wraps identifiers in brackets.
        /// </summary>
        /// <param name="identifier">The identifier to escape.</param>
        /// <param name="expected">The expected escaped identifier.</param>
        [Theory]
        [InlineData("MyTable", "[MyTable]")]
        [InlineData("my_table", "[my_table]")]
        [InlineData("[AlreadyBracketed]", "[AlreadyBracketed]")]
        public void EscapeIdentifier_ValidIdentifiers_ReturnsBracketed(string identifier, string expected)
        {
            // Act
            var result = SqlIdentifierValidator.EscapeIdentifier(identifier);

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that EscapeIdentifier handles closing brackets within identifiers.
        /// </summary>
        [Fact]
        public void EscapeIdentifier_IdentifierWithClosingBracket_EscapesBracket()
        {
            // Arrange
            var identifier = "Table]Name";

            // Act
            var result = SqlIdentifierValidator.EscapeIdentifier(identifier);

            // Assert
            Assert.Equal("[Table]]Name]", result);
        }

        /// <summary>
        /// Tests that ValidateAndEscape combines validation and escaping.
        /// </summary>
        /// <param name="identifier">The identifier to validate and escape.</param>
        /// <param name="expected">The expected result.</param>
        [Theory]
        [InlineData("MyTable", "[MyTable]")]
        [InlineData("my_table", "[my_table]")]
        [InlineData("Table123", "[Table123]")]
        public void ValidateAndEscape_ValidIdentifiers_ReturnsValidatedAndEscaped(string identifier, string expected)
        {
            // Act
            var result = SqlIdentifierValidator.ValidateAndEscape(identifier, "test");

            // Assert
            Assert.Equal(expected, result);
        }

        /// <summary>
        /// Tests that ValidateAndEscape rejects invalid identifiers.
        /// </summary>
        /// <param name="identifier">The invalid identifier.</param>
        [Theory]
        [InlineData("Table;DROP")]
        [InlineData("Table' OR '1'='1")]
        [InlineData("SELECT")]
        public void ValidateAndEscape_InvalidIdentifiers_ThrowsArgumentException(string identifier)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateAndEscape(identifier, "test"));
        }

        /// <summary>
        /// Tests that EscapeIdentifier rejects null or empty identifiers.
        /// </summary>
        /// <param name="identifier">The null or empty identifier.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EscapeIdentifier_NullOrEmpty_ThrowsArgumentException(string identifier)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.EscapeIdentifier(identifier));
        }

        /// <summary>
        /// Tests common SQL injection attack patterns are blocked.
        /// </summary>
        /// <param name="maliciousInput">The malicious input to test.</param>
        [Theory]
        [InlineData("'; DROP TABLE users; --")]
        [InlineData("1' OR '1'='1")]
        [InlineData("admin'--")]
        [InlineData("' OR 1=1--")]
        [InlineData("'; EXEC xp_cmdshell('dir'); --")]
        [InlineData("1; DELETE FROM users WHERE 1=1; --")]
        [InlineData("' UNION SELECT * FROM passwords--")]
        public void ValidateIdentifier_SqlInjectionPatterns_ThrowsArgumentException(string maliciousInput)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(maliciousInput, "test"));
            
            Assert.Contains("invalid characters", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Tests that special characters commonly used in SQL injection are blocked.
        /// </summary>
        /// <param name="identifier">The identifier with special characters.</param>
        [Theory]
        [InlineData("Table;")]
        [InlineData("Table'")]
        [InlineData("Table\"")]
        [InlineData("Table--")]
        [InlineData("Table/*")]
        [InlineData("Table*/")]
        [InlineData("Table(")]
        [InlineData("Table)")]
        [InlineData("Table=")]
        [InlineData("Table<")]
        [InlineData("Table>")]
        public void ValidateIdentifier_SpecialCharacters_ThrowsArgumentException(string identifier)
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                SqlIdentifierValidator.ValidateIdentifier(identifier, "test"));
        }
    }
}