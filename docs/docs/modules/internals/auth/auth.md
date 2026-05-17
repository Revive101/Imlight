# User Authentication
The original game launcher includes the `LOGIN` and `PATCH` [protocols](../systems/dml/protocols.md). There are a few records in the login protocol related to user authentication. It should be noted that most of these records have been deprecated, and the one utilized at the time of writing is `MSG_USER_AUTHEN_V3` and its associated `MSG_USER_AUTHEN_RSP` response.

```xml
<MSG_USER_AUTHEN_V3>
    <RECORD>
        <_MsgOrder TYPE="UBYT" NOXFER="TRUE">27</_MsgOrder>
        <_MsgName TYPE="STR" NOXFER="TRUE">MSG_USER_AUTHEN_V3</_MsgName>
        <_MsgDescription TYPE="STR" NOXFER="TRUE">User Authentication Request Message.</_MsgDescription>
        <_MsgHandler TYPE="STR" NOXFER="TRUE">MSG_UserAuthenV3</_MsgHandler>
        <_MsgAccessLvl TYPE="UBYT" NOXFER="TRUE">1</_MsgAccessLvl>
        <Rec1 TYPE="STR"/>
        <Version TYPE="STR"/>
        <Revision TYPE="STR"/>
        <DataRevision TYPE="STR"/>
        <CRC TYPE="STR"/>
        <MachineID TYPE="GID"/>
        <Locale TYPE="STR"/>
        <PatchClientID TYPE="STR"/>
        <IsSteamPatcher TYPE="UINT"/>
    </RECORD>
</MSG_USER_AUTHEN_V3>
```
__Figure A.I__ -- The `MSG_USER_AUTHEN_V3` DML record, as of writing.

::: tip
Note that it's also possible to trigger an internal login menu by leaving the `-U` flag absent for any [launch arguments](../launchargs.md).
:::

## Rec1
Most of these fields are used for client authentication, but the `Rec1` field will be the one to actually contain the session id, username, and password hash.

_Rec1_ is a homebrew algorithm, and the data is encrypted in TwoFish in OFB mode. Any server implementation should expect the data to be literally encrypted as `${SessionId} {Username} {PasswordHash}`.

```csharp
private const byte TWOFISH_BLOCK_SIZE = 0x10;
private const byte TWOFISH_KEY_SIZE = 2 * TWOFISH_BLOCK_SIZE;
private const byte TWOFISH_NONCE_SIZE = TWOFISH_BLOCK_SIZE;
private const byte KEY_CONSTANT = 0x17;
private const byte IV_CONSTANT = 0xB6;

public static ByteString Decode(byte[] encodedData, ushort sid, uint timeSecs, uint timeMillis)
{
    var key = DeriveTwofishKey(sid, timeSecs, timeMillis);
    var nonce = DeriveTwofishNonce();
    var cipher = CipherUtilities.GetCipher("Twofish/OFB/NoPadding");
    cipher.Init(false, new ParametersWithIV(new KeyParameter(key), nonce));

    return cipher.DoFinal(encodedData);
}

private static byte[] DeriveTwofishKey(ushort sessionID, uint timeSecs, uint timeMillis)
{
    var key = new byte[TWOFISH_KEY_SIZE];
    for (var i = 0; i < key.Length; i++)
    {
        key[i] = (byte)(KEY_CONSTANT + i);
    }
    key[4]  = (byte)(sessionID & 0xff);
    key[5]  = 0;
    key[6]  = (byte)((sessionID >> 8) & 0xff);
    key[8]  = (byte)(timeSecs & 0xff);
    key[9]  = (byte)((timeSecs >> 16) & 0xff);
    key[12] = (byte)((timeSecs >> 8)  & 0xff);
    key[13] = (byte)((timeSecs >> 24) & 0xff);
    key[14] = (byte)(timeMillis & 0xff);
    key[15] = (byte)((timeMillis >> 8) & 0xff);

    return key;
}

private static byte[] DeriveTwofishNonce()
{
    var iv = new byte[TWOFISH_NONCE_SIZE];
    for (var i = 0; i < iv.Length; i++)
    {
        iv[i] = (byte)(IV_CONSTANT - i);
    }

    return iv;
}
```
__Figure B.I__: `Rec1` method from Imlight's cryptography library.

## ClientKey1
The aforementioned password hash is another homebrew hashing algorithm, called _ClientKey1_ (or abbreviated to _CK1_). It is hashed with SHA512 alongside the session details given in `SessionOffer` control message when the [session](../systems/kinp/session.md) was created, and is base64 encoded.
```csharp
public static string HaskCK1(string input, ushort sessionID, uint timeSecs, uint timeMillis)
{
    var passwordHash = HashPassword(input);
    var salt = $"{sessionID}{timeSecs}{timeMillis}";

    return SecondaryEncrypt(passwordHash, salt);
}

private static string HashPassword(string password)
{
    using var sha512 = SHA512.Create();
    var passwordBytes = Encoding.UTF8.GetBytes(password);

    return Convert.ToBase64String(sha512.ComputeHash(passwordBytes));
}

public static string SecondaryEncrypt(string password, string seed)
{
    using var sha512 = SHA512.Create();
    var passwordBytes = Encoding.UTF8.GetBytes(password);
    var seedBytes = Encoding.UTF8.GetBytes(seed);
    var hash = sha512.ComputeHash(passwordBytes.Concat(seedBytes).ToArray());

    return Convert.ToBase64String(hash);
}
```
__Figure B.II__: `ClientKey1` method from Imlight's cryptography library.

## Transition
When the client sends `MSG_USER_AUTHEN_V3`, the server should answer with `MSG_USER_AUTHEN_RSP` indicating success or failure.

Following successful authentication, the message `MSG_USER_AUTHEN_RSP` contains a new 'Rec1' field that contains the session ID, username, and another hash known as _ClientKey2_. _ClientKey2_ is a server-only algorithm, and any subsequent server implementation may use any algorithm.

On user [validation](./validation.md), _ClientKey2_ is a session key hash that will be echoed back to the server.