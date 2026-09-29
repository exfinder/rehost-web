//------------------------------------------------------------------------------
// Compatibility implementation of the .NET Framework DataProtector contract.
// Behavior verified against .NET Framework 4.8.1 System.Security.dll.
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace System.Security.Cryptography {
    internal abstract class DataProtector {
        private const string InvalidAppNameOrPurposeMessage =
            "Application names and purposes must contain at least one character which is not white space.";
        private const string InvalidPurposeMessage =
            "The purpose of the protected blob does not match the expected purpose value of this data protector instance.";

        private string _applicationName;
        private string _primaryPurpose;
        private IEnumerable<string> _specificPurposes;
        private volatile byte[] _hashedPurpose;

        protected DataProtector(string applicationName, string primaryPurpose, string[] specificPurposes) {
            if (String.IsNullOrWhiteSpace(applicationName)) {
                throw new ArgumentException(InvalidAppNameOrPurposeMessage, nameof(applicationName));
            }
            if (String.IsNullOrWhiteSpace(primaryPurpose)) {
                throw new ArgumentException(InvalidAppNameOrPurposeMessage, nameof(primaryPurpose));
            }
            if (specificPurposes != null) {
                foreach (string purpose in specificPurposes) {
                    if (String.IsNullOrWhiteSpace(purpose)) {
                        throw new ArgumentException(InvalidAppNameOrPurposeMessage, nameof(specificPurposes));
                    }
                }
            }

            _applicationName = applicationName;
            _primaryPurpose = primaryPurpose;

            List<string> purposes = new List<string>();
            if (specificPurposes != null) {
                purposes.AddRange(specificPurposes);
            }
            _specificPurposes = purposes;
        }

        protected string ApplicationName {
            get { return _applicationName; }
        }

        protected virtual bool PrependHashedPurposeToPlaintext {
            get { return true; }
        }

        protected string PrimaryPurpose {
            get { return _primaryPurpose; }
        }

        protected IEnumerable<string> SpecificPurposes {
            get { return _specificPurposes; }
        }

        protected virtual byte[] GetHashedPurpose() {
            if (_hashedPurpose == null) {
                using (HashAlgorithm sha256 = SHA256.Create()) {
                    using (BinaryWriter writer = new BinaryWriter(
                        new CryptoStream(new MemoryStream(), sha256, CryptoStreamMode.Write),
                        new UTF8Encoding(false, true))) {
                        writer.Write(ApplicationName);
                        writer.Write(PrimaryPurpose);
                        foreach (string purpose in SpecificPurposes) {
                            writer.Write(purpose);
                        }
                    }
                    _hashedPurpose = sha256.Hash;
                }
            }

            return _hashedPurpose;
        }

        public abstract bool IsReprotectRequired(byte[] encryptedData);

        public static DataProtector Create(
            string providerClass,
            string applicationName,
            string primaryPurpose,
            params string[] specificPurposes) {
            if (providerClass == null) {
                throw new ArgumentNullException(nameof(providerClass));
            }

            return (DataProtector)CryptoConfig.CreateFromName(
                providerClass,
                applicationName,
                primaryPurpose,
                specificPurposes);
        }

        public byte[] Protect(byte[] userData) {
            if (userData == null) {
                throw new ArgumentNullException(nameof(userData));
            }

            if (PrependHashedPurposeToPlaintext) {
                byte[] hashedPurpose = GetHashedPurpose();
                byte[] dataWithPurpose = new byte[userData.Length + hashedPurpose.Length];
                Array.Copy(hashedPurpose, 0, dataWithPurpose, 0, hashedPurpose.Length);
                Array.Copy(userData, 0, dataWithPurpose, hashedPurpose.Length, userData.Length);
                userData = dataWithPurpose;
            }

            return ProviderProtect(userData);
        }

        protected abstract byte[] ProviderProtect(byte[] userData);

        protected abstract byte[] ProviderUnprotect(byte[] encryptedData);

        public byte[] Unprotect(byte[] encryptedData) {
            if (encryptedData == null) {
                throw new ArgumentNullException(nameof(encryptedData));
            }

            if (PrependHashedPurposeToPlaintext) {
                byte[] dataWithPurpose = ProviderUnprotect(encryptedData);
                byte[] hashedPurpose = GetHashedPurpose();

                if (dataWithPurpose.Length < hashedPurpose.Length ||
                    !CryptographicOperations.FixedTimeEquals(
                        hashedPurpose,
                        dataWithPurpose.AsSpan(0, hashedPurpose.Length))) {
                    throw new CryptographicException(InvalidPurposeMessage);
                }

                byte[] userData = new byte[dataWithPurpose.Length - hashedPurpose.Length];
                Array.Copy(dataWithPurpose, hashedPurpose.Length, userData, 0, userData.Length);
                return userData;
            }

            return ProviderUnprotect(encryptedData);
        }
    }
}
