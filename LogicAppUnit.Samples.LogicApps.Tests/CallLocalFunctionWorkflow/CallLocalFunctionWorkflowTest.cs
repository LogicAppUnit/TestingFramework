using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.CallLocalFunctionWorkflow
{
    /// <summary>
    /// Test cases for the <i>call-local-function-workflow</i> workflow which calls a local function that exists in the same Logic App.
    /// </summary>
    [TestClass]
    public class CallLocalFunctionWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.CALL_LOCAL_FUNCTION_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests the workflow when the calling of the local function is successful.
        /// This test can only be run on Windows because it calls a local function targetting the .NET Framework.
        /// </summary>
        [TestMethod]
        [TestCategory("WindowsOnly")]
        public void CallLocalFunctionWorkflowTest_When_Successful()
        {
            const string zipCode = "13579";
            const string tempScale = "Fahrenheit";

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .FromAction("Get_Weather_Forecast"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsJson(new {
                            ZipCode = zipCode,
                            CurrentWeather = "The current weather is 41 Fahrenheit",
                            DayLow = "The low for the day is 31 Fahrenheit",
                            DayHigh = "The high for the day is 51 Fahrenheit"
                        }));

                // Run the workflow
                Dictionary<string, string> queryParams = new()
                {
                    { "zipCode", zipCode },
                    { "tempScale", tempScale }
                };
                var workflowResponse = testRunner.TriggerWorkflow(queryParams, HttpMethod.Get);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                JObject responseContent = workflowResponse.Content.ReadAsAsync<JObject>().Result;
                Assert.AreEqual(zipCode, responseContent["ZipCode"].Value<string>());
                Assert.IsTrue(responseContent["CurrentWeather"].Value<string>().Contains(tempScale));
                Assert.IsTrue(responseContent["DayLow"].Value<string>().Contains(tempScale));
                Assert.IsTrue(responseContent["DayHigh"].Value<string>().Contains(tempScale));

                // Check the "Call a local Function" action
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Weather_Forecast"));

                // Check the "Call a local Function" action input
                JToken getWeatherForecastInput = testRunner.GetWorkflowActionInput("Get_Weather_Forecast");
                Assert.AreEqual("WeatherForecast", getWeatherForecastInput["body"]["functionName"].Value<string>());
                Assert.AreEqual(zipCode, getWeatherForecastInput["body"]["parameters"]["zipCode"].Value<string>());
                Assert.AreEqual(tempScale, getWeatherForecastInput["body"]["parameters"]["temperatureScale"].Value<string>());

                // Check the "Call a local Function" action output
                JToken getWeatherForecastOutput = testRunner.GetWorkflowActionOutput("Get_Weather_Forecast");
                Assert.AreEqual(zipCode, getWeatherForecastOutput["body"]["ZipCode"].Value<string>());
                Assert.IsTrue(getWeatherForecastOutput["body"]["CurrentWeather"].Value<string>().Contains(tempScale));
                Assert.IsTrue(getWeatherForecastOutput["body"]["DayLow"].Value<string>().Contains(tempScale));
                Assert.IsTrue(getWeatherForecastOutput["body"]["DayHigh"].Value<string>().Contains(tempScale));
            }
        }

        /// <summary>
        /// Tests the workflow when the calling of the local function fails with an exception
        /// This test can only be run on Windows because it calls a local function targetting the .NET Framework.
        /// </summary>
        [TestMethod]
        [TestCategory("WindowsOnly")]
        public void CallLocalFunctionWorkflowTest_When_Exception()
        {
            const string zipCode = "54321";
            const string tempScale = "Celsius";

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .FromAction("Get_Weather_Forecast"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .ThrowsException(new InvalidOperationException("Something went bang!")));

                // Run the workflow
                Dictionary<string, string> queryParams = new()
                {
                    { "zipCode", zipCode },
                    { "tempScale", tempScale }
                };
                var workflowResponse = testRunner.TriggerWorkflow(queryParams, HttpMethod.Get);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.InternalServerError, workflowResponse.StatusCode);

                // Check the "Call a local Function" action
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Get_Weather_Forecast"));

                // Check the "Call a local Function" action input
                JToken getWeatherForecastInput = testRunner.GetWorkflowActionInput("Get_Weather_Forecast");
                Assert.AreEqual("WeatherForecast", getWeatherForecastInput["body"]["functionName"].Value<string>());
                Assert.AreEqual(zipCode, getWeatherForecastInput["body"]["parameters"]["zipCode"].Value<string>());
                Assert.AreEqual(tempScale, getWeatherForecastInput["body"]["parameters"]["temperatureScale"].Value<string>());

                // The throwing of an exception in a local function does not generate an action output in the workflow.
                // Therefore we shouldn't be validating the action output in a test. We can only validate the action status (failed).
                // JToken getWeatherForecastOutput = testRunner.GetWorkflowActionOutput("Get_Weather_Forecast");
            }
        }
    }
}