using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests;

/// <summary>Verifica a validação declarativa de criação e edição de reembolsos.</summary>
[TestClass]
public sealed class ExpenseRequestValidationTests
{
    private static readonly DateOnly _validExpenseDate = new(2026, 10, 1);

    /// <summary>Um corpo completo e dentro dos limites passa na validação declarativa.</summary>
    [TestMethod]
    public void Request_AllFieldsValid_PassesDeclarativeValidation()
    {
        ExpenseRequest request = CreateValidRequest();

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);

        Assert.IsTrue(valid);
    }

    /// <summary>A categoria é obrigatória e deve ser maior que zero.</summary>
    /// <param name="categoryId">Valor inválido de categoria.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(0)]
    [DataRow(-1)]
    public void Request_InvalidCategoryId_FailsDeclarativeValidation(int? categoryId)
    {
        ExpenseRequest request = CreateValidRequest();
        request.CategoryId = categoryId;
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(ExpenseRequest.CategoryId))));
    }

    /// <summary>A descrição é obrigatória e deve ter entre dez e quinhentos caracteres.</summary>
    /// <param name="description">Valor inválido de descrição.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("Curta")]
    public void Request_InvalidDescription_FailsDeclarativeValidation(string? description)
    {
        ExpenseRequest request = CreateValidRequest();
        request.Description = description;
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(ExpenseRequest.Description))));
    }

    /// <summary>Uma descrição acima de quinhentos caracteres é recusada.</summary>
    [TestMethod]
    public void Request_DescriptionAboveMaximumLength_FailsDeclarativeValidation()
    {
        ExpenseRequest request = CreateValidRequest();
        request.Description = new string('a', 501);
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(ExpenseRequest.Description))));
    }

    /// <summary>Os limites inclusivos de descrição são aceitos.</summary>
    /// <param name="length">Comprimento válido no limite.</param>
    [TestMethod]
    [DataRow(10)]
    [DataRow(500)]
    public void Request_DescriptionAtLengthLimits_PassesDeclarativeValidation(int length)
    {
        ExpenseRequest request = CreateValidRequest();
        request.Description = new string('a', length);

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);

        Assert.IsTrue(valid);
    }

    /// <summary>O valor é obrigatório e deve estar entre R$ 0,01 e o limite do contrato.</summary>
    /// <param name="amount">Valor inválido do reembolso.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow(0.0)]
    [DataRow(-1.0)]
    [DataRow(2147483648.0)]
    public void Request_InvalidAmount_FailsDeclarativeValidation(double? amount)
    {
        ExpenseRequest request = CreateValidRequest();
        request.Amount = (decimal?)amount;
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(ExpenseRequest.Amount))));
    }

    /// <summary>Os limites inclusivos do valor monetário são aceitos.</summary>
    /// <param name="amount">Valor válido no limite.</param>
    [TestMethod]
    [DataRow(0.01)]
    [DataRow(2147483647.0)]
    public void Request_AmountAtLimits_PassesDeclarativeValidation(double amount)
    {
        ExpenseRequest request = CreateValidRequest();
        request.Amount = (decimal)amount;

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true);

        Assert.IsTrue(valid);
    }

    /// <summary>A data da despesa é obrigatória.</summary>
    [TestMethod]
    public void Request_MissingExpenseDate_FailsDeclarativeValidation()
    {
        ExpenseRequest request = CreateValidRequest();
        request.ExpenseDate = null;
        List<ValidationResult> errors = [];

        bool valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(e => e.MemberNames.Contains(nameof(ExpenseRequest.ExpenseDate))));
    }

    /// <summary>O DTO aceita apenas os campos do contrato e ignora identidade e estado enviados.</summary>
    [TestMethod]
    public void Request_SpoofedServerFields_AreIgnored()
    {
        const string json = """
            {
              "categoryId": 1,
              "description": "Almoço com cliente importante",
              "amount": 50.00,
              "expenseDate": "2026-10-01",
              "ownerId": "intruder",
              "status": "Paid",
              "id": "11111111-1111-1111-1111-111111111111"
            }
            """;
        System.Text.Json.JsonSerializerOptions options = new(System.Text.Json.JsonSerializerDefaults.Web);
        ExpenseRequest? request = System.Text.Json.JsonSerializer.Deserialize<ExpenseRequest>(json, options);

        Assert.IsNotNull(request);
        Assert.AreEqual(1, request.CategoryId);
        Assert.AreEqual("Almoço com cliente importante", request.Description);
        Assert.AreEqual(50.00m, request.Amount);
        Assert.AreEqual(_validExpenseDate, request.ExpenseDate);

        System.Text.Json.JsonElement serialized = System.Text.Json.JsonSerializer.SerializeToElement(request, options);
        Assert.HasCount(4, serialized.EnumerateObject());
        Assert.IsFalse(serialized.TryGetProperty("ownerId", out _));
        Assert.IsFalse(serialized.TryGetProperty("status", out _));
        Assert.IsFalse(serialized.TryGetProperty("id", out _));
    }

    private static ExpenseRequest CreateValidRequest()
    {
        return new ExpenseRequest
        {
            CategoryId = 1,
            Description = "Almoço com cliente importante",
            Amount = 50.00m,
            ExpenseDate = _validExpenseDate,
        };
    }
}
