using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// Oyuncu hareketini SAHIBININ makinesinde yetkili kilar.
///
/// NGO'nun standart NetworkTransform'u sunucu yetkilidir: sen yurursun, sunucuya
/// gider, sunucu onaylar, sana geri doner. Bu kendi karakterinde gecikme hissi
/// yaratir. Co-op bir oyunda hile kaygisi olmadigi icin hareketi oyuncunun
/// kendisine birakiyoruz -- kendi karakterin aninda tepki verir.
///
/// KULLANIM: Player prefab'ina NetworkTransform yerine BUNU ekle.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative() => false;
}
