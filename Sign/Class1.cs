using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using System.Security.Cryptography;
using System.Text;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Sign
{
    /// <summary>
    /// SHA哈希算法封装
    /// </summary>
    public class SHA : IDisposable
    {
        private HashAlgorithm _hashAlgorithm;

        /// <summary>
        /// 创建SHA1哈希算法实例
        /// </summary>
        public static SHA SHA1() => new(System.Security.Cryptography.SHA1.Create());

        /// <summary>
        /// 创建SHA256哈希算法实例
        /// </summary>
        public static SHA SHA256() => new(System.Security.Cryptography.SHA256.Create());

        private SHA(HashAlgorithm algorithm)
        {
            _hashAlgorithm = algorithm;
        }

        /// <summary>
        /// 使用新数据更新哈希状态
        /// </summary>
        public void Update(byte[] data)
        {
            _hashAlgorithm.TransformBlock(data, 0, data.Length, null, 0);
        }

        /// <summary>
        /// 获取哈希结果的字节长度
        /// </summary>
        public int OutputSize => _hashAlgorithm.HashSize / 8;

        /// <summary>
        /// 完成哈希计算并将结果写入提供的缓冲区
        /// </summary>
        public void FinalizeInto(byte[] output)
        {
            _hashAlgorithm.TransformFinalBlock([], 0, 0);
            Array.Copy(_hashAlgorithm.Hash, output, _hashAlgorithm.Hash.Length);
        }

        public void Dispose()
        {
            _hashAlgorithm?.Dispose();
        }
    }

    /// <summary>
    /// 提供哈希相关的工具函数
    /// </summary>
    public static class HashUtils
    {
        /// <summary>
        /// 获取SHA哈希算法实例
        /// </summary>
        public static SHA GetSHA(bool useSha1) => useSha1 ? SHA.SHA1() : SHA.SHA256();

        /// <summary>
        /// 计算数据的SHA1哈希
        /// </summary>
        public static void Sha1Hash(byte[] data, byte[] output)
        {
            byte[] hash = SHA1.HashData(data);
            Array.Copy(hash, output, hash.Length);
        }

        /// <summary>
        /// 计算数据的SHA256哈希
        /// </summary>
        public static void Sha256Hash(byte[] data, byte[] output)
        {
            byte[] hash = SHA256.HashData(data);
            Array.Copy(hash, output, hash.Length);
        }
    }

    /// <summary>
    /// 签名算法类型
    /// </summary>
    public enum SignatureAlgorithm
    {
        SHA256withRSA,
        SHA256withECDSA,
        SHA384withECDSA,
        SHA521withECDSA
    }

    /// <summary>
    /// 签名密钥
    /// </summary>
    public class SigningKey
    {
        public SignatureAlgorithm Algorithm { get; }
        public AsymmetricKeyParameter Key { get; }

        private SigningKey(SignatureAlgorithm algorithm, AsymmetricKeyParameter key)
        {
            Algorithm = algorithm;
            Key = key;
        }

        /// <summary>
        /// 从PKCS8格式的密钥数据创建签名密钥
        /// </summary>
        public static SigningKey FromPkcs8(byte[] keyData)
        {
            try
            {
                AsymmetricKeyParameter privateKey = PrivateKeyFactory.CreateKey(keyData);

                // 尝试RSA密钥
                if (privateKey is RsaPrivateCrtKeyParameters)
                {
                    return new SigningKey(SignatureAlgorithm.SHA256withRSA, privateKey);
                }

                // 尝试EC密钥
                if (privateKey is ECPrivateKeyParameters ecKey)
                {
                    var fieldSize = ecKey.Parameters.Curve.FieldSize;

                    if (fieldSize == 256)
                        return new SigningKey(SignatureAlgorithm.SHA256withECDSA, privateKey);
                    else if (fieldSize == 384)
                        return new SigningKey(SignatureAlgorithm.SHA384withECDSA, privateKey);
                    else if (fieldSize == 521)
                        return new SigningKey(SignatureAlgorithm.SHA521withECDSA, privateKey);
                }

                throw new ArgumentException("不支持的私钥格式");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"加载私钥时出错: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 验证密钥
    /// </summary>
    public class VerifyingKey
    {
        public SignatureAlgorithm Algorithm { get; }
        public AsymmetricKeyParameter Key { get; }

        private VerifyingKey(SignatureAlgorithm algorithm, AsymmetricKeyParameter key)
        {
            Algorithm = algorithm;
            Key = key;
        }

        /// <summary>
        /// 从公钥信息创建验证密钥
        /// </summary>
        public static VerifyingKey FromPublicKeyInfo(SubjectPublicKeyInfo publicKeyInfo)
        {
            try
            {
                var key = PublicKeyFactory.CreateKey(publicKeyInfo);

                // 判断密钥类型
                if (key is RsaKeyParameters)
                {
                    return new VerifyingKey(SignatureAlgorithm.SHA256withRSA, key);
                }
                else if (key is ECPublicKeyParameters ecKey)
                {
                    var fieldSize = ecKey.Parameters.Curve.FieldSize;

                    if (fieldSize == 256)
                        return new VerifyingKey(SignatureAlgorithm.SHA256withECDSA, key);
                    else if (fieldSize == 384)
                        return new VerifyingKey(SignatureAlgorithm.SHA384withECDSA, key);
                    else if (fieldSize == 521)
                        return new VerifyingKey(SignatureAlgorithm.SHA521withECDSA, key);
                }

                throw new ArgumentException("不支持的公钥格式");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"加载公钥时出错: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 签名验证器
    /// </summary>
    public class Verifier
    {
        private IDigest _digest;
        private readonly VerifyingKey _key;

        private Verifier(IDigest digest, VerifyingKey key)
        {
            _digest = digest;
            _key = key;
        }

        /// <summary>
        /// 从公钥创建验证器
        /// </summary>
        public static Verifier FromPublicKey(SubjectPublicKeyInfo publicKeyInfo)
        {
            try
            {
                var key = VerifyingKey.FromPublicKeyInfo(publicKeyInfo);
                IDigest digest = key.Algorithm switch
                {
                    SignatureAlgorithm.SHA256withRSA or SignatureAlgorithm.SHA256withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha256Digest(),
                    SignatureAlgorithm.SHA384withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha384Digest(),
                    SignatureAlgorithm.SHA521withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha512Digest(),
                    _ => throw new ArgumentException("不支持的签名算法"),
                };
                return new Verifier(digest, key);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"创建验证器失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用数据更新验证器状态
        /// </summary>
        public void Update(byte[] data)
        {
            _digest.BlockUpdate(data, 0, data.Length);
        }

        /// <summary>
        /// 验证签名
        /// </summary>
        public bool Verify(byte[] signature)
        {
            try
            {
                byte[] hash = new byte[_digest.GetDigestSize()];
                _digest.DoFinal(hash, 0);
                ISigner signer = _key.Algorithm switch
                {
                    SignatureAlgorithm.SHA256withRSA => SignerUtilities.GetSigner("SHA256withRSA"),
                    SignatureAlgorithm.SHA256withECDSA => SignerUtilities.GetSigner("SHA256withECDSA"),
                    SignatureAlgorithm.SHA384withECDSA => SignerUtilities.GetSigner("SHA384withECDSA"),
                    SignatureAlgorithm.SHA521withECDSA => SignerUtilities.GetSigner("SHA512withECDSA"),// SHA-521实际使用SHA-512
                    _ => throw new ArgumentException("不支持的签名算法"),
                };
                signer.Init(false, _key.Key);
                signer.BlockUpdate(hash, 0, hash.Length);

                return signer.VerifySignature(signature);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"签名验证失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 签名生成器
    /// </summary>
    public class Signer
    {
        private IDigest _digest;
        private readonly SigningKey _key;

        private Signer(IDigest digest, SigningKey key)
        {
            _digest = digest;
            _key = key;
        }

        /// <summary>
        /// 从私钥创建签名生成器
        /// </summary>
        public static Signer FromPrivateKey(byte[] keyData)
        {
            try
            {
                var key = SigningKey.FromPkcs8(keyData);
                IDigest digest = key.Algorithm switch
                {
                    SignatureAlgorithm.SHA256withRSA or SignatureAlgorithm.SHA256withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha256Digest(),
                    SignatureAlgorithm.SHA384withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha384Digest(),
                    SignatureAlgorithm.SHA521withECDSA => new Org.BouncyCastle.Crypto.Digests.Sha512Digest(),
                    _ => throw new ArgumentException("不支持的签名算法"),
                };
                return new Signer(digest, key);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"创建签名器失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用数据更新签名器状态
        /// </summary>
        public void Update(byte[] data)
        {
            _digest.BlockUpdate(data, 0, data.Length);
        }

        /// <summary>
        /// 生成签名
        /// </summary>
        public byte[] Sign()
        {
            try
            {
                byte[] hash = new byte[_digest.GetDigestSize()];
                _digest.DoFinal(hash, 0);
                ISigner signer = _key.Algorithm switch
                {
                    SignatureAlgorithm.SHA256withRSA => SignerUtilities.GetSigner("SHA256withRSA"),
                    SignatureAlgorithm.SHA256withECDSA => SignerUtilities.GetSigner("SHA256withECDSA"),
                    SignatureAlgorithm.SHA384withECDSA => SignerUtilities.GetSigner("SHA384withECDSA"),
                    SignatureAlgorithm.SHA521withECDSA => SignerUtilities.GetSigner("SHA512withECDSA"),// SHA-521实际使用SHA-512
                    _ => throw new ArgumentException("不支持的签名算法"),
                };
                signer.Init(true, _key.Key);
                signer.BlockUpdate(hash, 0, hash.Length);

                return signer.GenerateSignature();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"生成签名失败: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// 已验证的属性
    /// </summary>
    public class AuthenticatedAttributes
    {
        public string Target { get; }
        public long Length { get; }

        public AuthenticatedAttributes(string target, long length)
        {
            Target = target;
            Length = length;
        }

        /// <summary>
        /// 将对象转换为DER编码
        /// </summary>
        public byte[] ToDer()
        {
            var seq = new DerSequence(
                new DerPrintableString(Target),
                new DerInteger(Length)
            );

            return seq.GetEncoded();
        }

        /// <summary>
        /// 从DER序列解析对象
        /// </summary>
        public static AuthenticatedAttributes FromDerSequence(Asn1Sequence seq)
        {
            var target = DerPrintableString.GetInstance(seq[0]).GetString();
            var length = DerInteger.GetInstance(seq[1]).Value.LongValue;

            return new AuthenticatedAttributes(target, length);
        }
    }

    /// <summary>
    /// 引导镜像签名
    /// </summary>
    public class BootSignature
    {
        public int FormatVersion { get; set; }
        public X509Certificate Certificate { get; set; }
        public AlgorithmIdentifier AlgorithmIdentifier { get; set; }
        public AuthenticatedAttributes AuthenticatedAttributes { get; set; }
        public byte[] Signature { get; set; }

        /// <summary>
        /// 从DER编码创建引导签名对象
        /// </summary>
        public static BootSignature FromDer(byte[] der)
        {
            try
            {
                var seq = Asn1Sequence.GetInstance(der);

                var formatVersion = DerInteger.GetInstance(seq[0]).Value.IntValue;
                var certificate = new X509Certificate(seq[1].ToAsn1Object().GetEncoded());
                var algorithmIdentifier = AlgorithmIdentifier.GetInstance(seq[2]);

                var attrSeq = Asn1Sequence.GetInstance(seq[3]);
                var authAttrs = AuthenticatedAttributes.FromDerSequence(attrSeq);
                var signature = Asn1OctetString.GetInstance(seq[4]).GetOctets();

                return new BootSignature
                {
                    FormatVersion = formatVersion,
                    Certificate = certificate,
                    AlgorithmIdentifier = algorithmIdentifier,
                    AuthenticatedAttributes = authAttrs,
                    Signature = signature
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"解析BootSignature失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 将对象转换为DER编码
        /// </summary>
        public byte[] ToDer()
        {
            try
            {
                var attrSeq = new DerSequence(
                    new DerPrintableString(AuthenticatedAttributes.Target),
                    new DerInteger(AuthenticatedAttributes.Length)
                );

                var seq = new DerSequence(
                    new DerInteger(FormatVersion),
                    Certificate.CertificateStructure,
                    AlgorithmIdentifier,
                    attrSeq,
                    new DerOctetString(Signature)
                );

                return seq.GetEncoded();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"编码BootSignature失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 验证签名是否有效
        /// </summary>
        public bool Verify(byte[] payload)
        {
            try
            {
                if (AuthenticatedAttributes.Length != payload.Length)
                {
                    Console.Error.WriteLine("镜像大小无效");
                    return false;
                }

                var publicKeyInfo = Certificate.CertificateStructure.SubjectPublicKeyInfo;
                var verifier = Verifier.FromPublicKey(publicKeyInfo);

                verifier.Update(payload);
                verifier.Update(AuthenticatedAttributes.ToDer());

                return verifier.Verify(Signature);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"验证失败: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// 字节数据源
    /// </summary>
    public class BytesSource
    {
        private readonly byte[] _data;

        private BytesSource(byte[] data)
        {
            _data = data;
        }

        public static BytesSource FromFile(string path)
        {
            return new BytesSource(File.ReadAllBytes(path));
        }

        public static BytesSource FromBytes(byte[] data)
        {
            return new BytesSource(data);
        }

        public byte[] AsBytes() => _data;
    }

    /// <summary>
    /// 引导镜像签名工具
    /// </summary>
    public static class BootSignatureUtils
    {
        // 内置的默认密钥和证书
        private static readonly byte[] VERITY_PEM = File.ReadAllBytes("tools/keys/verity.x509.pem");
        private static readonly byte[] VERITY_PK8 = File.ReadAllBytes("tools/keys/verity.pk8");

        /// <summary>
        /// 验证引导镜像签名
        /// </summary>
        public static bool VerifyBootImage(BootImage img, string cert = null)
        {
            try
            {
                byte[] tail = img.GetTail();

                // 查找DER序列的开始位置
                int startIndex = 0;
                while (startIndex < tail.Length && tail[startIndex] != 0x30)
                    startIndex++;

                if (startIndex >= tail.Length)
                {
                    Console.Error.WriteLine("无效的签名格式");
                    return false;
                }

                var sig = BootSignature.FromDer([.. tail.Skip(startIndex)]);

                if (!string.IsNullOrEmpty(cert))
                {
                    using var reader = new StreamReader(cert);
                    var pemReader = new PemReader(reader);
                    var certObj = pemReader.ReadObject();
                    if (certObj is X509Certificate x509Cert)
                    {
                        sig.Certificate = x509Cert;
                    }
                    else
                    {
                        Console.Error.WriteLine("无效的证书格式");
                        return false;
                    }
                }

                return sig.Verify(img.GetPayload());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"引导镜像验证出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 为引导镜像创建签名
        /// </summary>
        public static byte[] SignBootImage(byte[] payload, string name, string cert = null, string key = null)
        {
            try
            {
                // 处理参数
                BytesSource certSource;
                BytesSource keySource;

                if (string.IsNullOrEmpty(cert))
                    certSource = BytesSource.FromBytes(VERITY_PEM);
                else
                    certSource = BytesSource.FromFile(cert);

                if (string.IsNullOrEmpty(key))
                    keySource = BytesSource.FromBytes(VERITY_PK8);
                else
                    keySource = BytesSource.FromFile(key);

                // 解析证书和私钥
                X509Certificate certificate;
                using (var reader = new StringReader(Encoding.UTF8.GetString(certSource.AsBytes())))
                {
                    var pemReader = new PemReader(reader);
                    certificate = (X509Certificate)pemReader.ReadObject();
                }

                var signer = Signer.FromPrivateKey(keySource.AsBytes());

                // 签名镜像
                var attr = new AuthenticatedAttributes(name, payload.Length);
                signer.Update(payload);
                signer.Update(attr.ToDer());
                var signature = signer.Sign();

                // 创建BootSignature DER
                var bootSig = new BootSignature
                {
                    FormatVersion = 1,
                    Certificate = certificate,
                    AlgorithmIdentifier = certificate.CertificateStructure.SignatureAlgorithm,
                    AuthenticatedAttributes = attr,
                    Signature = signature
                };

                return bootSig.ToDer();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"引导镜像签名出错: {ex.Message}");
                return [];
            }
        }
    }

    /// <summary>
    /// 引导镜像类(模拟原C++类的接口)
    /// </summary>
    public class BootImage
    {
        private byte[] _data;
        private int _payloadSize;

        public BootImage(byte[] data, int payloadSize)
        {
            _data = data;
            _payloadSize = payloadSize;
        }

        public byte[] GetPayload()
        {
            byte[] payload = new byte[_payloadSize];
            Array.Copy(_data, payload, _payloadSize);
            return payload;
        }

        public byte[] GetTail()
        {
            byte[] tail = new byte[_data.Length - _payloadSize];
            Array.Copy(_data, _payloadSize, tail, 0, tail.Length);
            return tail;
        }
    }
}