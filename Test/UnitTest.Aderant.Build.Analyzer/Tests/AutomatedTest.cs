using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class AutomatedTest {
        #region Types

        /// <summary>
        /// A data object containing information necessary to succinctly execute tests.
        /// </summary>
        private struct TestData {
            public TestData(
                string className,
                object instance,
                Tuple<MethodInfo, MethodInfo, MethodInfo[]> testClassMethods) {
                ClassName = className;
                Instance = instance;
                Initialize = testClassMethods.Item1;
                Cleanup = testClassMethods.Item2;
                Tests = testClassMethods.Item3;
            }

            public string ClassName { get; }
            public object Instance { get; }
            public MethodInfo Initialize { get; }
            public MethodInfo Cleanup { get; }
            public MethodInfo[] Tests { get; }
        }

        /// <summary>
        /// A data object contianing information relevant to a failed test.
        /// </summary>
        private class ErrorData {
            public ErrorData(
                Exception exception,
                string message,
                string stackTrace,
                string testClass,
                string testName) {
                Exception = exception;
                Message = message;
                StackTrace = stackTrace;
                TestClass = testClass;
                TestName = testName;
            }

            public Exception Exception { get; }
            public string Message { get; }
            public string StackTrace { get; }
            public string TestClass { get; }
            public string TestName { get; }
        }

        #endregion Types

        /// <summary>
        /// This single test will dynamically load each Roslyn Rule class,
        /// and then run every rule against every test of every other rule.
        /// This ensures that every rule passes every test,
        /// and that all test code is valid code according to every rule.
        /// The entire process is automatic.
        /// </summary>
        [TestMethod]
        public void Analyzer_AutomatedTest() {
            var testData = GetTestData();

            var errorData = new IEnumerable<ErrorData>[testData.Length];

            for (int i = 0; i < testData.Length; ++i) {
                errorData[i] = ExecuteTests(testData[i]);
            }

            var output = FormatRawOutput(errorData);

            if (output != null) {
                throw new InternalTestFailureException(output);
            }
        }

        /// <summary>
        /// Executes all of the tests for the specified test data.
        /// </summary>
        /// <param name="testData">The test data.</param>
        private static IEnumerable<ErrorData> ExecuteTests(TestData testData) {
            var length = testData.Tests.Length;

            var results = new ErrorData[length];

            // Execute each test using the provided test data as context.
            for (int i = 0; i < length; ++i) {
                results[i] = ExecuteTest(testData.Tests[i], testData);
            }

            return results.Where(data => data != null);
        }

        /// <summary>
        /// Executes the specified test using the specified data.
        /// </summary>
        /// <param name="test">The test.</param>
        /// <param name="testData">The test data.</param>
        private static ErrorData ExecuteTest(MethodBase test, TestData testData) {
            try {
                // Test Initialize
                testData.Initialize?.Invoke(testData.Instance, Array.Empty<object>());

                // Test
                test.Invoke(testData.Instance, Array.Empty<object>());

                // Test Cleanup
                testData.Cleanup?.Invoke(testData.Instance, Array.Empty<object>());
            } catch (Exception ex) {
                // Unroll exception stack.
                var exception = ex;

                while (true) {
                    if (exception.InnerException == null) {
                        break;
                    }

                    exception = exception.InnerException;
                }

                // Amalgamate error data.
                return new ErrorData(
                    exception,
                    exception.Message,
                    exception.StackTrace,
                    testData.ClassName,
                    test.Name);
            }

            return null;
        }

        /// <summary>
        /// Formats the raw test execution output into a single error message string.
        /// </summary>
        /// <param name="rawOutput">The raw output.</param>
        private static string FormatRawOutput(IEnumerable<IEnumerable<ErrorData>> rawOutput) {
            var messages = new List<string>();

            foreach (var testClassOutput in rawOutput) {
                messages.AddRange(
                    testClassOutput.Select(
                        output =>
                            $"Class: {output.TestClass}\n" +
                            $"Test: {output.TestName}\n" +
                            $"Exception: {output.Exception.GetType().Name}\n" +
                            $"Error: {output.Message}\n" +
                            $"Stack Trace:\n{output.StackTrace}\n"));
            }

            return messages.Count > 0
                ? "Failed Tests: " + messages.Count + "\n\n" + string.Join("\n", messages)
                : null;
        }

        /// <summary>
        /// Set up and return all requisite data necessary to execute every rules test.
        /// </summary>
        private static TestData[] GetTestData() {
            // Get the test types.
            var validTypes = GetValidTestClassTypes();

            // Get the rule instances.
            var injectedRules = GetInjectedRules();

            // Retrieve test class methods from the specified test types,
            // combine test class methods, test class type name,
            // and rule instance into a single data object.
            var testData = new TestData[validTypes.Count];

            for (int i = 0; i < testData.Length; ++i) {
                var instance = Activator.CreateInstance(
                    validTypes[i],
                    new object[] { injectedRules });

                testData[i] = new TestData(
                    validTypes[i].FullName,
                    instance,
                    GetTestClassMethods(validTypes[i]));
            }

            return testData;
        }

        /// <summary>
        /// Returns the type for each [TestClass] located within the test assembly,
        /// after ensuring that each type contains the required constructor.
        /// </summary>
        private static IReadOnlyList<Type> GetValidTestClassTypes() {
            // Get test class types from the test assembly.
            var testClassTypes = GetAllTestClassTypes();

            List<Type> invalidTypes = null;
            var validTypes = new List<Type>(testClassTypes.Count);
            var parameterType = typeof(RuleBase[]);

            // Iterate through each test class type, and confirm they have the requisite constructor.
            foreach (var type in testClassTypes) {
                bool isValid = false;

                // Constructor iteration.
                foreach (var constructor in type.GetConstructors()) {
                    var parameters = constructor.GetParameters();

                    // A 'valid' type has a constructor with a single parameter of type 'RuleBase[]'.
                    if (parameters.Length != 1 || parameters[0].ParameterType != parameterType) {
                        continue;
                    }

                    isValid = true;
                    break;
                }

                // Assign the type to valid or invalid as required.
                if (isValid) {
                    validTypes.Add(type);
                    continue;
                }

                if (invalidTypes == null) {
                    invalidTypes = new List<Type>();
                }

                invalidTypes.Add(type);
            }

            // If there are no invalid types, return.
            if (invalidTypes == null) {
                return validTypes;
            }

            // Compose a string detailing the invalid types.
            string invalidTypesString = string.Join(", ", invalidTypes.Select(type => type.FullName));

            // Raise an exception for the invalid types.
            throw new InternalTestFailureException(
                "The following Unit Test classes are missing a required constructor: " + invalidTypesString);
        }

        /// <summary>
        /// Returns the type for each [TestClass] located within the test assembly.
        /// </summary>
        private static IReadOnlyList<Type> GetAllTestClassTypes() {
            const string testAssemblyString = "UnitTest.Aderant.Build.Analyzer";

            var testClassAttribute = typeof(TestClassAttribute);

            // Load, retrieve, and filter all types found within the test assembly.
            List<Type> types;
            try {
                types = Assembly.Load(testAssemblyString).GetTypes().ToList();
            } catch (ReflectionTypeLoadException exception) {
                types = exception.Types.Where(type => type != null).ToList();
            }

            var testClasses = types
                .Where(type =>
                    type.IsClass &&
                    type.GetCustomAttribute(testClassAttribute) != null &&
                    type.IsSubclassOf(typeof(AderantCodeFixVerifier)))
                .ToList();

            // Ensure a test class was found.
            if (testClasses.Count < 1) {
                throw new InternalTestFailureException("No Unit Tests were found in: " + testAssemblyString);
            }

            return testClasses;
        }

        /// <summary>
        /// Returns an instance of every class in the analyzer assembly,
        /// that derives from <seealso cref="RuleBase"/>.
        /// </summary>
        private static RuleBase[] GetInjectedRules() {
            // Get the types of every rule in the analyser assembly.
            var ruleTypes = GetRuleTypes();

            var injectedRules = new RuleBase[ruleTypes.Count];

            // Create instances of every rule type.
            for (int i = 0; i < ruleTypes.Count; ++i) {
                var instance = Activator.CreateInstance(ruleTypes[i]) as RuleBase;

                injectedRules[i] = instance ?? throw new InternalTestFailureException($"Failed to create instance of rule '{ruleTypes[i].FullName}'.");
            }

            return injectedRules;
        }

        /// <summary>
        /// Returns a list of concrete types in the analyzer assembly
        /// that derive from <seealso cref="RuleBase"/>.
        /// </summary>
        private static IReadOnlyList<Type> GetRuleTypes() {
            // Retrieve all types from the Analyzer assembly.
            const string rulesAssemblyString = "Aderant.Build.Analyzer";

            List<Type> types;
            try {
                types = Assembly.Load(rulesAssemblyString).GetTypes().ToList();
            } catch (ReflectionTypeLoadException exception) {
                types = exception.Types.Where(type => type != null).ToList();
            }

            var ruleBaseType = typeof(RuleBase);

            var ruleTypes = new List<Type>(types.Count);

            // Filter types and ignore types that are not classes, or are not concrete.
            var filteredTypes = types.Where(type => type.IsClass && !type.IsAbstract);

            // Iterate through the filtered types.
            foreach (var type in filteredTypes) {
                var baseType = type.BaseType;

                // Loop to evaluate nested base types.
                while (true) {
                    if (baseType == null) {
                        break;
                    }

                    if (baseType == ruleBaseType) {
                        ruleTypes.Add(type);
                        break;
                    }

                    baseType = baseType.BaseType;
                }
            }

            // Ensure rules were found.
            if (ruleTypes.Count < 1) {
                throw new InternalTestFailureException("No rules were found in: " + rulesAssemblyString);
            }

            return ruleTypes;
        }

        /// <summary>
        /// Returns test-associated methods from the specified class type.
        /// Methods returned:
        ///     Item1: [TestInitialize] method, if one exists.
        ///     Item2: [TestCleanup] method, if one exists.
        ///     Item3: All [TestMethod] methods that exist.
        /// </summary>
        /// <param name="testClass">The test class.</param>
        private static Tuple<MethodInfo, MethodInfo, MethodInfo[]> GetTestClassMethods(IReflect testClass) {
            // Target method attributes.
            var testInitializeAttribute = typeof(TestInitializeAttribute);
            var testMethodAttribute = typeof(TestMethodAttribute);
            var testCleanupAttribute = typeof(TestCleanupAttribute);

            MethodInfo initialize = null;
            MethodInfo cleanup = null;
            var typeMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            // Assume that every public instance method on the provided type is a test,
            // allocate collection capacity accordingly.
            var tests = new List<MethodInfo>(typeMethods.Length);

            // Iterate through every public instance method on the provided type.
            foreach (var method in typeMethods) {
                if (method.GetCustomAttribute(testMethodAttribute) != null) {
                    // [TestMethod] methods are the most common, and is therefore listed first.
                    tests.Add(method);
                } else if (initialize == null && method.GetCustomAttribute(testInitializeAttribute) != null) {
                    // The [TestInitialize] method.
                    initialize = method;
                } else if (cleanup == null && method.GetCustomAttribute(testCleanupAttribute) != null) {
                    // The [TestCleanup] method.
                    cleanup = method;
                }
            }

            // Bundle data into a single result object.
            return new Tuple<MethodInfo, MethodInfo, MethodInfo[]>(initialize, cleanup, tests.ToArray());
        }

    }
}
