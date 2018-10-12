using System;
using System.Linq;
using System.Activities;
using System.Activities.Statements;
using System.Activities.DurableInstancing;
using System.Data.SqlClient;
using System.Data;
using System.Collections.Generic;
using System.Threading;

namespace CustomerWorkflow
{
    class Program
    {
        public struct Customer
        {
            public string Name { get; set; }
            public string ActiveBookmark { get; set; }
            public string InstanceId { get; set; }
            public string WorkflowId { get; set; }
        }

        const string connectionString = "Server=.\\SQLEXPRESS;Initial Catalog=CustomerWorkflow;Integrated Security=SSPI";
        static List<Customer> customers = new List<Customer>();
        static AutoResetEvent instanceUnloaded = new AutoResetEvent(true);

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: CustomerWorkflow.exe <command>");
                return;
            }

            GetCustomers();

            string cmd = args[0];
            switch( cmd )
            {
                case "list":
                    foreach(Customer c in customers)
                        Console.WriteLine(c.Name + " " + c.ActiveBookmark + " " + c.InstanceId);
                    break;
                case "create":
                    CreateInstance(args[1]);
                    break;
                case "send":
                    SendInstance(args[1], args[2]);
                    break;
            }

            instanceUnloaded.WaitOne();
        }

        private static void GetCustomers()
        {
            using (SqlConnection localCon = new SqlConnection(connectionString))
            {
                string localCmd =
                    "SELECT c.Name, c.InstanceId, i.ActiveBookmarks, c.WorkflowId from dbo.[Customer] c LEFT JOIN [System.Activities.DurableInstancing].[Instances] i ON i.InstanceId = c.InstanceId ORDER BY [CreationTime]";

                SqlCommand cmd = localCon.CreateCommand();
                cmd.CommandText = localCmd;
                localCon.Open();
                using (SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    while (reader.Read())
                    {
                        string name = reader[0].ToString();
                        string guidString = reader[1].ToString();
                        string activeBookmark = reader[2].ToString();
                        string workflowId = reader[3].ToString();
                        customers.Add(new Customer { Name = name, InstanceId = guidString, ActiveBookmark = activeBookmark, WorkflowId = workflowId });
                    }
                }
            }
        }

        private static Activity GetWorkflow(string workflowId)
        {
            Activity workflow = null;
            switch(workflowId)
            {
                case "Workflow1":
                    workflow = new Workflow1();
                    break;
                case "Workflow2":
                    workflow = new Workflow2();
                    break;
            }
            return workflow;
        }

        private static void CreateInstance(string customerName)
        {
            if(!customers.Exists(c => c.Name == customerName))
            {
                Console.WriteLine(string.Format("No such customer {0}.", customerName));
                return;
            }

            var customer = customers.Find(c => c.Name == customerName);
            if ( customer.InstanceId != "")
            {
                Console.WriteLine(string.Format("Customer {0} already has a workflow instance.", customerName));
                return;
            }

            SqlWorkflowInstanceStore store = new SqlWorkflowInstanceStore(connectionString);
            WorkflowApplication.CreateDefaultInstanceOwner(store, null, WorkflowIdentityFilter.Any);

            Activity workflow = GetWorkflow(customer.WorkflowId);
            if( workflow == null)
            {
                Console.WriteLine(string.Format("Unknown workflow {0}", customer.WorkflowId));
                return;
            }

            WorkflowApplication wfApp = new WorkflowApplication(workflow);
            UpdateCustomer(customerName, wfApp.Id);
            wfApp.InstanceStore = store;
            ConfigureWFApp(wfApp, customer);
            instanceUnloaded.Reset();
            wfApp.Run();
        }

        private static void SendInstance(string customerName, string bookmarkName)
        {
            if (!customers.Exists(c => c.Name == customerName))
            {
                Console.WriteLine(string.Format("No such customer {0}.", customerName));
                return;
            }

            var customer = customers.Find(c => c.Name == customerName);
            if (customer.InstanceId == "")
            {
                Console.WriteLine(string.Format("Customer {0} does not have workflow instance.", customerName));
                return;
            }

            SqlWorkflowInstanceStore store = new SqlWorkflowInstanceStore(connectionString);
            WorkflowApplication.CreateDefaultInstanceOwner(store, null, WorkflowIdentityFilter.Any);

            WorkflowApplicationInstance instance =
                WorkflowApplication.GetInstance(new Guid(customer.InstanceId), store);

            Activity wf = GetWorkflow(customer.WorkflowId);
            if (wf == null)
            {
                Console.WriteLine(string.Format("Unknown workflow {0}", customer.WorkflowId));
                return;
            }
            WorkflowApplication wfApp =
                new WorkflowApplication(wf, instance.DefinitionIdentity);

            ConfigureWFApp(wfApp, customer);
            wfApp.Load(instance);
            instanceUnloaded.Reset();
            wfApp.ResumeBookmark(bookmarkName, null);
        }

        private static void ConfigureWFApp(WorkflowApplication wfApp, Customer customer)
        {
            wfApp.Aborted = delegate (WorkflowApplicationAbortedEventArgs e)
            {
                Console.WriteLine(string.Format("Workflow Aborted. Exception: {0}\r\n{1}",
                        e.Reason.GetType().FullName,
                        e.Reason.Message));
            };
            wfApp.PersistableIdle = delegate (WorkflowApplicationIdleEventArgs e)
            {
                Console.WriteLine("PersistableIdle");
                return PersistableIdleAction.Unload;
            };
            wfApp.Unloaded = (workflowApplicationEventArgs) =>
            {
                Console.WriteLine("Unloaded");
                instanceUnloaded.Set();
            };
            wfApp.OnUnhandledException = delegate (WorkflowApplicationUnhandledExceptionEventArgs e)
            {
                Console.WriteLine(e.ExceptionSource);
                return UnhandledExceptionAction.Cancel;
            };
            wfApp.Completed = delegate (WorkflowApplicationCompletedEventArgs e)
            {
                if (e.CompletionState == ActivityInstanceState.Faulted)
                {
                    Console.WriteLine(string.Format("Workflow Terminated. Exception: {0}\r\n{1}",
                        e.TerminationException.GetType().FullName,
                        e.TerminationException.Message));
                }
                else if (e.CompletionState == ActivityInstanceState.Canceled)
                {
                    Console.WriteLine("Workflow Canceled.");
                }
                else
                {
                    UpdateCustomer(customer.Name, Guid.Empty);
                }
            };
        }

        private static void UpdateCustomer(string customerName, Guid instanceId)
        {
            using (SqlConnection localCon = new SqlConnection(connectionString))
            {
                string localCmd =
                    string.Format("UPDATE customer SET InstanceId = {0} WHERE Name = '{1}'", (instanceId == Guid.Empty) ? "null" : "'" + instanceId.ToString() + "'", customerName);

                SqlCommand cmd = localCon.CreateCommand();
                cmd.CommandText = localCmd;
                localCon.Open();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
