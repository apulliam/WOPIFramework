using System.Net.Http;
using System.Web.Http;
using System.Threading.Tasks;
using Microsoft.Dx.Wopi;
using Microsoft.Dx.Wopi.Models;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using Microsoft.Dx.Wopi.Security;

namespace Microsoft.Dx.Wopi.Controllers
{
    public abstract class WopiFilesController : ApiController
    {
        [HttpGet]
        [Route("wopi/files/{file_id}")]
        public async Task<HttpResponseMessage> CheckFileInfo(string file_id)
        {
            var checkFileInfoRequest = new CheckFileInfoRequest(this.Request, file_id);
            WopiResponse wopiResponse = null;
            try
            {
                if (await Authorize(checkFileInfoRequest))
                {
                    if (await WopiProof.Validate(checkFileInfoRequest))
                    {
                        wopiResponse = await CheckFileInfo(checkFileInfoRequest);
                    }
                    else
                    {
                        wopiResponse = checkFileInfoRequest.ResponseServerError("Proof validation failed");
                    }
                }
                else
                {
                    wopiResponse = checkFileInfoRequest.ResponseUnauthorized();
                }
            }
            catch (Exception ex)
            {
                wopiResponse = checkFileInfoRequest.ResponseServerError(ex.Message);
            }
            return wopiResponse.ToHttpResponse();
        }

        [HttpGet]
        [Route("wopi/files/{file_id}/contents")]
        public async Task<HttpResponseMessage> GetFile(string file_id)
        {
            var getFileRequest = new GetFileRequest(this.Request, file_id);
            WopiResponse wopiResponse = null;
            try
            {
                if (await Authorize(getFileRequest))
                {
                    if (await WopiProof.Validate(getFileRequest))
                    {
                        wopiResponse = await GetFile(getFileRequest);
                    }
                    else
                    {
                        getFileRequest.ResponseServerError("Proof validation failed");
                    }
                }
                else
                {
                    wopiResponse = getFileRequest.ResponseUnauthorized();
                }
            }
            catch (Exception ex)
            {
                wopiResponse = getFileRequest.ResponseServerError(ex.Message);
            }

            return wopiResponse.ToHttpResponse();
        }

        [HttpPost]
        [Route("wopi/files/{file_id}")]
        public async Task<HttpResponseMessage> ProcessPostActions(string file_id)
        {
            WopiRequest wopiRequest = new WopiRequest(this.Request, file_id);
            WopiResponse wopiResponse = null;     
            try
            {
                if (await Authorize(wopiRequest))
                {
                    if (await WopiProof.Validate(wopiRequest))
                    {
                        var filesPostOverride = WopiRequest.GetHttpRequestHeader(this.Request, WopiRequestHeaders.OVERRIDE);

                        switch (filesPostOverride)
                        {

                            case "LOCK":
                                var oldLock = WopiRequest.GetHttpRequestHeader(this.Request, WopiRequestHeaders.OLD_LOCK);
                                if (oldLock != null)
                                {
                                    wopiResponse = await UnlockAndRelock(new UnlockAndRelockRequest(this.Request, file_id));
                                }
                                else
                                { 
                                    wopiResponse = await Lock(new LockRequest(this.Request, file_id));
                                }
                                break;
                            case "GET_LOCK":
                                wopiResponse = await GetLock(new GetLockRequest(this.Request, file_id));
                                break;
                            case "REFRESH_LOCK":
                                wopiResponse = await RefreshLock(new RefreshLockRequest(this.Request, file_id));
                                break;
                            case "UNLOCK":
                                wopiResponse = await Unlock(new UnlockRequest(this.Request, file_id));
                                break;
                            case "PUT_RELATIVE_FILE":
                                var suggestedTarget = WopiRequest.GetHttpRequestHeader(this.Request, WopiRequestHeaders.SUGGESTED_TARGET);
                                var relativeTarget = WopiRequest.GetHttpRequestHeader(this.Request, WopiRequestHeaders.RELATIVE_TARGET);
                                if (suggestedTarget != null && relativeTarget != null)
                                {
                                    // This really should be BadRequest, but the spec requires NotImplmented
                                    wopiResponse = new WopiResponse()
                                    {
                                        StatusCode = HttpStatusCode.BadRequest
                                    };
                                }
                                else
                                {
                                    if (suggestedTarget != null)
                                        wopiResponse = await PutRelativeFileSuggested(new PutRelativeFileSuggestedRequest(this.Request, file_id));
                                    else if (relativeTarget != null)
                                        wopiResponse = await PutRelativeFileSpecific(new PutRelativeFileSpecificRequest(this.Request, file_id));
                                    else // Both are null
                                        wopiResponse =  new WopiResponse()
                                        {
                                            StatusCode = HttpStatusCode.NotImplemented
                                        };
                                }
                                break;
                            case "RENAME_FILE":
                                wopiResponse = await RenameFile(new RenameFileRequest(this.Request, file_id));
                                break;
                            case "PUT_USER_INFO":
                                wopiResponse = await PutUserInfo(new PutUserInfoRequest(this.Request, file_id));
                                break;
                            default:
                                wopiResponse = wopiRequest.ResponseServerError(string.Format("Invalid {0} header value: {1}", WopiRequestHeaders.OVERRIDE, filesPostOverride));
                                break;
                        }
                    }
                    else
                    {
                        wopiResponse = wopiRequest.ResponseServerError("Proof validation failed");
                    }
                }
                else
                {
                    wopiResponse = wopiRequest.ResponseUnauthorized();
                }
            }
            catch (Exception ex)
            {
                wopiResponse = wopiRequest.ResponseServerError(ex.Message);
            }
            return wopiResponse.ToHttpResponse();
        }


        [HttpPost]
        [Route("wopi/files/{file_id}/contents")]
        public async Task<HttpResponseMessage> PutFile(string file_id)
        {
            var putFileRequest = new PutFileRequest(this.Request, file_id);
            WopiResponse wopiResponse = null;
            try
            {
                if (await Authorize(putFileRequest))
                {
                    if (await WopiProof.Validate(putFileRequest))
                    {
                        wopiResponse = await PutFile(putFileRequest);
                    }
                    else
                    {
                        wopiResponse = putFileRequest.ResponseServerError("Proof validation failed");
                    }
                }
                else
                {
                    wopiResponse = putFileRequest.ResponseUnauthorized();
                }
            }
            catch (Exception ex)
            {
                wopiResponse = putFileRequest.ResponseServerError(ex.Message);
            }
            return wopiResponse.ToHttpResponse();
        }
       
        public abstract Task<bool> Authorize(WopiRequest wopiRequest);
      
        public abstract Task<WopiResponse> CheckFileInfo(CheckFileInfoRequest checkFileInfoRequest);
       
        public abstract Task<WopiResponse> GetFile(GetFileRequest getFileRequest);

        public abstract Task<WopiResponse> Lock(LockRequest lockRequest);

        public abstract Task<WopiResponse> Unlock(UnlockRequest unlockRequest);

        public abstract Task<WopiResponse> UnlockAndRelock(UnlockAndRelockRequest unlockRequest);

        public abstract Task<WopiResponse> RefreshLock(RefreshLockRequest refreshLock);

        public abstract Task<WopiResponse> GetLock(GetLockRequest getLockRequest);

        public abstract Task<WopiResponse> PutRelativeFileSpecific(PutRelativeFileSpecificRequest putRelativeFileSpecificRequest);

        public abstract Task<WopiResponse> PutRelativeFileSuggested(PutRelativeFileSuggestedRequest putRelativeFileSuggestedRequest);

        public abstract Task<WopiResponse> RenameFile(RenameFileRequest renameFileRequest);

        public abstract Task<WopiResponse> PutUserInfo(PutUserInfoRequest putUserInfoRequest);

        public abstract Task<WopiResponse> PutFile(PutFileRequest putFileRequest);
    }
}
