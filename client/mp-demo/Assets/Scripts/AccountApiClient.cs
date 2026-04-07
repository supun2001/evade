using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class AccountData
{
    public string username;
    public int money;
    public int[] ownedSkinIndices;
    public int equippedSkinIndex;
}

[Serializable]
public class AccountAuthResponse
{
    public bool ok;
    public string token;
    public string error;
    public AccountData account;
}

[Serializable]
public class AccountResponse
{
    public bool ok;
    public string error;
    public AccountData account;
}

[Serializable]
public class AuthRequestBody
{
    public string username;
    public string password;
}

[Serializable]
public class SkinActionRequestBody
{
    public int skinIndex;
}

public static class AccountApiClient
{
    public static async Task<AccountAuthResponse> RegisterAsync(string baseUrl, string username, string password)
    {
        return await SendRequestAsync<AccountAuthResponse>(
            $"{baseUrl}/auth/register",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(new AuthRequestBody
            {
                username = username,
                password = password,
            }),
            null);
    }

    public static async Task<AccountAuthResponse> LoginAsync(string baseUrl, string username, string password)
    {
        return await SendRequestAsync<AccountAuthResponse>(
            $"{baseUrl}/auth/login",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(new AuthRequestBody
            {
                username = username,
                password = password,
            }),
            null);
    }

    public static async Task<AccountResponse> GetAccountAsync(string baseUrl, string token)
    {
        return await SendRequestAsync<AccountResponse>(
            $"{baseUrl}/auth/me",
            UnityWebRequest.kHttpVerbGET,
            null,
            token);
    }

    public static async Task<AccountResponse> PurchaseSkinAsync(string baseUrl, string token, int skinIndex)
    {
        return await SendRequestAsync<AccountResponse>(
            $"{baseUrl}/shop/purchase-skin",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(new SkinActionRequestBody
            {
                skinIndex = skinIndex,
            }),
            token);
    }

    public static async Task<AccountResponse> EquipSkinAsync(string baseUrl, string token, int skinIndex)
    {
        return await SendRequestAsync<AccountResponse>(
            $"{baseUrl}/shop/equip-skin",
            UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(new SkinActionRequestBody
            {
                skinIndex = skinIndex,
            }),
            token);
    }

    private static async Task<T> SendRequestAsync<T>(string url, string method, string jsonBody, string token)
        where T : class, new()
    {
        using UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();

        if (!string.IsNullOrEmpty(jsonBody))
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        UnityWebRequestAsyncOperation operation = request.SendWebRequest();
        while (!operation.isDone)
        {
            await Task.Yield();
        }

        string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
        T parsed = ParseJson<T>(responseText) ?? new T();

        if (request.result == UnityWebRequest.Result.Success)
        {
            return parsed;
        }

        return parsed;
    }

    private static T ParseJson<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"AccountApiClient: failed to parse {typeof(T).Name}: {exception.Message}");
            return null;
        }
    }
}
