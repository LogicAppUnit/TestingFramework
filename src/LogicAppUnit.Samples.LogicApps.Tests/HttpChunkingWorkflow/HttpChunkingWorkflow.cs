﻿using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.HttpChunkingWorkflow
{
    /// <summary>
    /// Test cases for the <i>http-chunking-workflow</i> workflow which uses a chunked transfer mode within HTTP action.
    /// </summary>
    [TestClass]
    public class HttpChunkingWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, "http-chunking-workflow");
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        [TestMethod]
        public void ChunkedTransferWorkflow_Success()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/data"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsJson(GetDataResponse()));
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.Exact, "/api/v1.1/upload"))
                    .RespondWithDefault();

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.Accepted, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Action"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Post_Action"));
            }
        }

        private static dynamic GetDataResponse()
        {
            return new
            {
                id = 54624,
                title = "Mr",
                firstName = "Peter",
                lastName = "Smith",
                dateOfBirth = "1970-04-25",
                languageCode = "en-GB",
                address = new
                {
                    line1 = "8 High Street",
                    line2 = (string)null,
                    line3 = (string)null,
                    town = "Luton",
                    county = "Bedfordshire",
                    postcode = "LT12 6TY",
                    countryCode = "UK",
                    countryName = "United Kingdom"
                },
                extra = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer et nisl in tellus sodales aliquet in id sem. Suspendisse cursus mollis erat eu ullamcorper. Nulla congue id odio at facilisis. Sed ultrices dolor nisi, sit amet cursus leo pellentesque eget. Praesent sagittis ligula leo. Vestibulum varius eros posuere tortor tristique eleifend. Praesent ornare accumsan nisi sed auctor. Fusce ullamcorper nisi nec mi euismod, in efficitur quam volutpat.Vestibulum at iaculis felis. Fusce augue sem, efficitur ut vulputate quis, cursus nec mi. Nulla sagittis posuere ornare. Morbi lectus eros, luctus non condimentum eget, pretium eget sem. Aliquam convallis sed sem accumsan ultricies. Quisque commodo at odio sit amet iaculis. Curabitur nec lectus vel leo tristique aliquam et a ipsum. Duis tortor augue, gravida sed dui ac, feugiat pulvinar ex. Integer luctus urna at mauris feugiat, nec mattis elit mattis. Fusce dictum odio quis semper blandit. Pellentesque nunc augue, elementum sit amet nunc et."
            };
        }
    }
}