# WopiFramework
This repository contains a simple framework for building a WOPI host with ASP.NET, along with two sample reference implementations - one using DocumentDB and one using SQL Server.   

 For information on the what, why and how around the WOPI Framework, [see my blog post](https://blogs.msdn.microsoft.com/apulliam/2016/06/19/wopi-framework/).

> NOTE: You cannot simply clone and run this sample locally. Integrating with Office Online requires that your host domain is white-listed by Microsoft. The first step to white-listing a domain is to join the Cloud Storage Provider Program detail [HERE](http://dev.office.com/programs/officecloudstorage "HERE"). Additionally, a WOPI host must expose endpoints to the internet that Office Online can communicate with (read: localhost probably won't work).
