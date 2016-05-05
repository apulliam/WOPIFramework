using Microsoft.Dx.Wopi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Security
{
    internal class WopiProof
    {

        public string oldvalue { get; set; }
        public string oldmodulus { get; set; }
        public string oldexponent { get; set; }
        public string value { get; set; }
        public string modulus { get; set; }
        public string exponent
        {
            get; set;
        }
        /// <summary>
        /// Validates the WOPI Proof on an incoming WOPI request
        /// </summary>
        public async static Task<bool> Validate(WopiRequest wopiRequest)
        {
            var hostUrl = wopiRequest.RequestUri.OriginalString.Replace(":44300", "").Replace(":443", "");

            // Make sure the request has the correct headers
            if (wopiRequest.Proof == null ||
                wopiRequest.Timestamp == null)
                return false;

            // Set the requested proof values
            var requestProof = wopiRequest.Proof;
            var requestProofOld = String.Empty;
            if (wopiRequest.ProofOld != null)
                requestProofOld = wopiRequest.ProofOld;

            // Get the WOPI proof info from discovery
            var discoProof = await WopiDiscovery.getWopiProof();

            // Encode the values into bytes
            var accessTokenBytes = Encoding.UTF8.GetBytes(wopiRequest.AccessToken);
            var hostUrlBytes = Encoding.UTF8.GetBytes(hostUrl.ToUpperInvariant());
            var timeStampBytes = BitConverter.GetBytes(Convert.ToInt64(wopiRequest.Timestamp)).Reverse().ToArray();

            // Build expected proof
            List<byte> expected = new List<byte>(
                4 + accessTokenBytes.Length +
                4 + hostUrlBytes.Length +
                4 + timeStampBytes.Length);

            // Add the values to the expected variable
            expected.AddRange(BitConverter.GetBytes(accessTokenBytes.Length).Reverse().ToArray());
            expected.AddRange(accessTokenBytes);
            expected.AddRange(BitConverter.GetBytes(hostUrlBytes.Length).Reverse().ToArray());
            expected.AddRange(hostUrlBytes);
            expected.AddRange(BitConverter.GetBytes(timeStampBytes.Length).Reverse().ToArray());
            expected.AddRange(timeStampBytes);
            byte[] expectedBytes = expected.ToArray();

            return (verifyProof(expectedBytes, requestProof, discoProof.value) ||
                verifyProof(expectedBytes, requestProof, discoProof.oldvalue) ||
                verifyProof(expectedBytes, requestProofOld, discoProof.value));
        }

        /// <summary>
        /// Verifies the proof against a specified key
        /// </summary>
        private static bool verifyProof(byte[] expectedProof, string proofFromRequest, string proofFromDiscovery)
        {
            using (RSACryptoServiceProvider rsaProvider = new RSACryptoServiceProvider())
            {
                try
                {
                    rsaProvider.ImportCspBlob(Convert.FromBase64String(proofFromDiscovery));
                    return rsaProvider.VerifyData(expectedProof, "SHA256", Convert.FromBase64String(proofFromRequest));
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (CryptographicException)
                {
                    return false;
                }
            }
        }
    }
}
