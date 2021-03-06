﻿using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;

namespace HomeExercises
{
    public class NumberValidatorTests
    {
        [TestCase(-1, 1, "non-positive precision doesn't make sense",
            TestName = "{m}NonPositivePrecision")]
        [TestCase(1, -1, "negative fraction part length doesn't make sense", 
            TestName = "{m}NegativeFraction")]
        [TestCase(1, 2, "length of fraction part can not be greater than total number length", 
            TestName = "{m}ScaleGreaterThanPrecision")]
        public void ValidatorConstructor_IsThrowingException_On(
            int precision, int scale, string message)
        {
            Action act = () => new NumberValidator(precision, scale);
            act.ShouldThrow<ArgumentException>($"{message} (N({precision}, {scale}))");
        }

        [TestCase(1, 0, "some numbers can have zero-length fraction part (i.e. integers)", 
            TestName = "{m}ZeroScale")]
        public void ValidatorConstructor_IsWorkingProperly_On(
            int precision, int scale, string message)
        {
            Action act = () => new NumberValidator(precision, scale);
            act.ShouldNotThrow<ArgumentException>($"{message} (N({precision}, {scale}))");
        }

        [TestCase("0", "it's a valid number", TestName ="{m}IsValidNumber")]
        [TestCase("0.0", "numbers can have fraction part", TestName = "{m}HasFractionPart")]
        [TestCase("+0.0", "plus sign is an acceptable starting symbol",
            TestName = "{m}StartsWithPlusSign")]
        [TestCase("-0.0", "minus sign is an acceptable starting symbol", 
            TestName = "{m}StartsWithMinusSign")]
        [TestCase("0,0", "fraction can also be denoted by ,",
            TestName = "{m}HasFractionDenotedByComma")]
        public void IsValidNumber_ShouldBeTrue_IfFormat(
            string number, string message, int precision=3, int scale=2, bool onlyPositive=false)
        {
            var validator = new NumberValidator(precision, scale, onlyPositive);
            var validatorStr = $"{(onlyPositive ? "positive-only " : "")}N({precision},{scale})";
            validator.IsValidNumber(number).Should().BeTrue(
               $"{message} (on {validatorStr} validator for \"{number}\")");
        }


        //incorrect number formats
        [TestCase(null, "null must not be validated", TestName = "{m}IsNull")]
        [TestCase("", "empty string is not a number string", TestName = "{m}IsEmpty")]
        [TestCase(" ", "whitespace is not a number string", TestName = "{m}IsWhitespace")]
        [TestCase("a.sd", "number string must not consist of letters", 
            TestName = "{m}ConsistOfLetters")]
        [TestCase(" 0.0",
    "number strings must not start with whitespace(s) or any other characters", 
            TestName = "{m}StartsWithWhitespace")]
        [TestCase("0.0 ",
    "number strings must not end with whitespace(s) or any other characters", 
            TestName = "{m}EndsWithWhitespace")]
        [TestCase("++0", "number string must not have more than one sign symbol", 
            TestName = "{m}HasMoreThanOneSign")]
        [TestCase("0.0.0", "number string must not have more than one dot/comma", 
            TestName = "{m}HasMoreThanOneDelimiter")]
        [TestCase(".1", "integer part must be present", 
            TestName = "{m}HasNoIntegerPartBeforeDelimiter")]
        [TestCase("0.", "fraction part must be present if there's a dot or comma", 
            TestName = "{m}HasNoFractionPartAfterDelimiter")]
        //with larger precision or scale
        [TestCase("00.00", "number string length must be less or equal to validator precision", 
            TestName = "{m}IsLongerThanPrecision")]
        [TestCase("0.000", 
            "fraction part length must be less or equal to validator fraction part precision", 
            TestName = "{m}HasFractionLongerThanScale")]
        [TestCase("-0.00", "minus sign must be accounted for when checking number precision", 
            TestName = "{m}IsLongerThanPrecisionWithMinusSign")]
        [TestCase("+0.00", "plus sign must be accounted for when checking number precision",
            TestName = "{m}IsLongerThanPrecisionWithPlusSign")]
        //integer validator
        [TestCase("0.1", "non-integer number should not be validated", 3, 0, 
            TestName = "{m}IsNotIntegerOnIntegerValidator")]
        [TestCase("1.0", "integer numbers with fraction part present must not be validated", 3, 0, 
            TestName = "{m}HasFractionOnIntegerValidator")]
        //positive-only validator
        [TestCase("-1", "negative number should not be validated", 3, 2, true, 
            TestName = "{m}IsNegativeOnPositiveOnlyValidator")]
        public void IsValidNumber_ShouldBeFalse_IfFormat(
            string number, string message, int precision=3, int scale=2, bool onlyPositive=false)
        {
            var validator = new NumberValidator(precision, scale, onlyPositive);
            var validatorStr = $"{(onlyPositive?"positive-only ":"")}N({precision},{scale})";
            validator.IsValidNumber(number).Should().BeFalse(
               $"{message} (on {validatorStr} validator for \"{number}\")");
        }
    }

    public class NumberValidator
    {
        private readonly Regex numberRegex;
        private readonly bool onlyPositive;
        private readonly int precision;
        private readonly int scale;

        public NumberValidator(int precision, int scale = 0, bool onlyPositive = false)
        {
            this.precision = precision;
            this.scale = scale;
            this.onlyPositive = onlyPositive;
            if (precision <= 0)
                throw new ArgumentException("precision must be a positive number");
            if (scale < 0 || scale >= precision)
                throw new ArgumentException("precision must be a non-negative number less or equal than precision");
            numberRegex = new Regex(@"^([+-]?)(\d+)([.,](\d+))?$", RegexOptions.IgnoreCase);
        }

        public bool IsValidNumber(string value)
        {
            // Проверяем соответствие входного значения формату N(m,k), в соответствии с правилом, 
            // описанным в Формате описи документов, направляемых в налоговый орган в электронном виде по телекоммуникационным каналам связи:
            // Формат числового значения указывается в виде N(m.к), где m – максимальное количество знаков в числе, включая знак (для отрицательного числа), 
            // целую и дробную часть числа без разделяющей десятичной точки, k – максимальное число знаков дробной части числа. 
            // Если число знаков дробной части числа равно 0 (т.е. число целое), то формат числового значения имеет вид N(m).
            if (string.IsNullOrEmpty(value))
                return false;

            var match = numberRegex.Match(value);
            if (!match.Success)
                return false;

            // Знак и целая часть
            var intPart = match.Groups[1].Value.Length + match.Groups[2].Value.Length;
            // Дробная часть
            var fracPart = match.Groups[4].Value.Length;

            if (intPart + fracPart > precision || fracPart > scale)
                return false;

            if (onlyPositive && match.Groups[1].Value == "-")
                return false;
            return true;
        }
    }
}  