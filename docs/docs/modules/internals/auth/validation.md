# Validation
_Validation_ occurs when a game client already expects an authorized session to be active server-side. A session key is echoed back to the server as received during user [authentication](./auth.md) to continue it's session.

```xml
<MSG_USER_VALIDATE>
    <RECORD>
      <_MsgOrder TYPE="UBYT" NOXFER="TRUE">15</_MsgOrder>
      <_MsgName TYPE="STR" NOXFER="TRUE">MSG_USER_VALIDATE</_MsgName>
      <_MsgDescription TYPE="STR" NOXFER="TRUE">User Validation Request Message.</_MsgDescription>
      <_MsgHandler TYPE="STR" NOXFER="TRUE">MSG_UserValidate</_MsgHandler>
      <_MsgAccessLvl TYPE="UBYT" NOXFER="TRUE">1</_MsgAccessLvl>
      <UserID TYPE="GID"></UserID>
      <PassKey3 TYPE="STR"></PassKey3>
      <MachineID TYPE="GID"></MachineID>
      <Locale TYPE="STR"></Locale>
      <PatchClientID TYPE="STR"></PatchClientID>
    </RECORD>
</MSG_USER_VALIDATE>
```
__Figure A.I__ -- The `MSG_USER_VALIDATE` DML record, as of writing.

### PassKey3
PassKey3 is another hashing algorithm used by the game client to securely trasmit said data over the [network](../systems/kinp/index.md).

It ia hashed using SHA512 and the initial input is the session key (or _ClientKey2_) that was given in user [authentication](./auth.md). 

It is then salted again with a string formatted as `$"{sessionID}{timeSecs}{timeMillis}"`, where `timeSecs` and `timeMillis` are the established times received from the clients's [SessionAccept](../systems/kinp/controlmessages.md#session-accept) message when the [session](../systems/kinp/session.md) was created.

```csharp
public static string EncodePK3(string input, ushort sessionID, uint timeSecs, uint timeMillis)
{
    using var sha512 = SHA512.Create();

    // Hash the original input, which should be ClientKey2
    var bytes = Encoding.UTF8.GetBytes(input);
    sha512.TransformBlock(bytes, 0, bytes.Length, null, 0);

    // Salt
    var sessionInfoBytes = Encoding.UTF8.GetBytes($"{sessionID}{timeSecs}{timeMillis}");
    sha512.TransformFinalBlock(sessionInfoBytes, 0, sessionInfoBytes.Length);

    var passkey3Bytes = sha512.Hash;
    var passkey3 = Convert.ToBase64String(passkey3Bytes);

    return passkey3;
}
```
__Figure B.I__ -- `PassKey3` method from Imlight's cryptography library.