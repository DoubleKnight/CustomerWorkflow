To configure the program, you need to create a SQL Server database (Express is fine), named "CustomerWorkflow"

Then you run these scripts in the newly created db, available on your Windows computer in the folder C:\Windows\Microsoft.NET\Framework64\v4.0.30319\SQL\en:
SqlWorkflowInstanceStoreSchema.sql
SqlWorkflowInstanceStoreLogic.sql

and also this script:
USE [CustomerWorkflow]
GO

/****** Object:  Table [dbo].[Customer]    Script Date: 2018-10-12 15:40:35 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Customer](
	[Name] [nvarchar](50) NOT NULL,
	[InstanceId] [uniqueidentifier] NULL,
	[WorkflowId] [nvarchar](50) NULL
) ON [PRIMARY]

GO

INSERT [dbo].[Customer] VALUES('Microsoft', NULL, 'Workflow1')
INSERT [dbo].[Customer] VALUES('Apple', NULL, 'Workflow2')

GO


