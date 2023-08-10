using Imlight.Server.Game.Models;
using Imlight.Server.Login.Models;
using Imlight.Server.WizardData.Implementations;
using WizUnraveler.ObjectProperty;
using Imlight.Common.Utilities;
using static WizUnraveler.Cache.TypeCache;

namespace Imlight.Server.WizardData;

public static class DatabaseUtilities
{
    private const string destinyRawData = "4C8F6E110100000000007200000000000" +
                                          "00000000000000000000052078DD072100" +
                                          "000CA186380310000000088BEC104000B0" +
                                          "0000000610001000300000000000000000" +
                                          "00000000000000000840CAB0400002300";
    private const string kevinRawData = "4C8F6E1101000000000072000000" +
                                        "0000000000000000000000000052" +
                                        "078DD072200000B5882240110100" +
                                        "000088BEC1040000000000001B0A" +
                                        "C079000000000000000000000000" +
                                        "000000000000B336F80400005D00";
    
    /// <summary>
    /// Creates and returns a fake account with random details.
    /// </summary>
    /// <returns></returns>
    public static Account CreateFakeDatabaseAccount()
    {
        // Create a new debug account.
        var userName = Faker.Internet.UserName();
        var email = Faker.Internet.Email();
        var password = Faker.Identification.SocialSecurityNumber();
        var fakeAcc = new Account(userName, email, password);
        
        // Destiny
        var destiny = GetCharacterFromRawCreationData(destinyRawData);
        fakeAcc.AddCharacter(destiny);
        
        // I fucking hate Kevin.
        var kevin = GetCharacterFromRawCreationData(kevinRawData);
        fakeAcc.AddCharacter(kevin);
        
        // Add the fake account to the database.
        AccountCollection.CreateAccount(fakeAcc);
        return fakeAcc;
    }

    /// <summary>
    /// Creates an account from given details. Persists immediately.
    /// </summary>
    /// <param name="username"></param>
    /// <param name="email"></param>
    /// <param name="password"></param>
    /// <param name="auth"></param>
    /// <returns></returns>
    public static Account CreateDatabaseAccount(
        string username,
        string email, 
        string password, 
        AuthLevel auth = AuthLevel.None)
    {
        var acc = new Account(username, email, password) { AuthLevel = auth };
        
        // Destiny
        var destiny = GetCharacterFromRawCreationData(destinyRawData);
        acc.AddCharacter(destiny);
        
        // I fucking hate Kevin.
        var kevin = GetCharacterFromRawCreationData(kevinRawData);
        acc.AddCharacter(kevin);
        
        // Save the account to the database.
        AccountCollection.CreateAccount(acc);

        return acc;
    }

    public static Character GetCharacterFromRawCreationData(string rawData)
    {
        var serializer = new ObjectSerializer();
        var destinyRawBytes = DataManipulation.StringToByteArray(rawData);
        var destinyCreationData = (WizardCharacterCreationInfo)serializer.Deserialize(destinyRawBytes);
        var destiny = new Character(destinyCreationData);

        return destiny;
    }
}