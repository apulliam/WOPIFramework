# WopiFramework
This repository contains a SDK for building WOPI hosts.  It is based on the WOPI Host reference implementation 
[HERE](https://github.com/OfficeDev/PnP-WOPI "HERE")  This SDK isolates WOPI Host implementors from having to deal with all of the peculiarities of the WOPI protocol.  Instead, a WOPI Host can be deployed by simply deriving from WopiFilesController and 
overriding the abstract methods.  These methods work with specially constructed WopiRequest and WopiResponse objects that only provide access to the correct options for each WOPI API method.

##Warning: This code is currently in development and is not fully debugged yet.

> NOTE: You cannot simply clone and run this sample locally. Integrating with Office Online requires that your host domain is white-listed by Microsoft. The first step to white-listing a domain is to join the Cloud Storage Provider Program detail [HERE](http://dev.office.com/programs/officecloudstorage "HERE"). Additionally, a WOPI host must expose endpoints to the internet that Office Online can communicate with (read: localhost probably won't work).
