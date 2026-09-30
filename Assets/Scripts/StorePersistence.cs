using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Preparation의 매장 상태만 JSON 파일 하나에 저장하고 복원한다.
// 고객, 계산 줄, 일일 통계는 저장하지 않는다.
public class StorePersistence : MonoBehaviour
{
    const int LegacySaveVersion = 1;
    const int LayoutSaveVersion = 2;
    const int ProductSaveVersion = 3;
    const int SaveVersion = 4;
    const string SaveFileName = "restock_save.json";

    [SerializeField] StoreSession session;
    [SerializeField] StoreEconomy economy;
    [SerializeField] StoreInventory inventory;
    [SerializeField] StorePricing pricing;
    [SerializeField] StoreStatistics statistics;
    [SerializeField] ProductDefinition[] products;
    [SerializeField] Shelf[] shelves;
    [SerializeField] BuildModeController buildMode;
    [SerializeField] StoreUpgradeSystem upgradeSystem;

    public bool SaveFileExists => File.Exists(SaveFilePath);

    string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public bool TrySave()
    {
        if (!CanPersist("저장"))
        {
            return false;
        }

        if (!TryCreateSaveData(out StoreSaveData data))
        {
            return false;
        }

        string json = JsonUtility.ToJson(data, true);
        string path = SaveFilePath;
        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StorePersistence: 저장 파일을 쓰지 못했습니다. {exception.Message}", this);
            return false;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception)
                {
                }
            }
        }

        Debug.Log($"Restock save written: {path}", this);
        return true;
    }

    public bool TryLoad()
    {
        if (!CanPersist("불러오기"))
        {
            return false;
        }

        string path = SaveFilePath;
        if (!File.Exists(path))
        {
            Debug.LogWarning("StorePersistence: 저장 파일이 없습니다.", this);
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StorePersistence: 저장 파일을 읽지 못했습니다. {exception.Message}", this);
            return false;
        }

        StoreSaveData data;
        try
        {
            data = JsonUtility.FromJson<StoreSaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"StorePersistence: 저장 파일을 해석하지 못했습니다. {exception.Message}", this);
            return false;
        }

        if (!TryValidateSaveData(data, out ValidatedSave validated, out string error))
        {
            Debug.LogWarning($"StorePersistence: 저장 파일을 적용하지 않습니다. {error}", this);
            return false;
        }

        if (!ApplyValidatedSave(validated))
        {
            return false;
        }

        Debug.Log($"Restock save loaded: {path}", this);
        return true;
    }

    bool CanPersist(string action)
    {
        if (session == null || economy == null || inventory == null || pricing == null || statistics == null)
        {
            Debug.LogWarning($"StorePersistence: 저장에 필요한 매장 컴포넌트가 연결되지 않아 {action}하지 않습니다.", this);
            return false;
        }

        if (session.Phase != StorePhase.Preparation)
        {
            Debug.LogWarning($"StorePersistence: Preparation에서만 {action}할 수 있습니다. 현재 상태: {session.Phase}", this);
            return false;
        }

        return TryValidateConfiguration(out string error) || FailConfiguration(error);
    }

    bool FailConfiguration(string error)
    {
        Debug.LogWarning($"StorePersistence: 저장 구성이 올바르지 않습니다. {error}", this);
        return false;
    }

    bool TryCreateSaveData(out StoreSaveData data)
    {
        data = null;
        if (!TryBuildProductMap(out Dictionary<string, ProductDefinition> productsById))
        {
            return false;
        }

        var inventoryEntries = new List<ProductQuantitySaveData>(products.Length);
        var priceEntries = new List<ProductPriceSaveData>(products.Length);
        for (int index = 0; index < products.Length; index++)
        {
            ProductDefinition product = products[index];
            if (!pricing.TryGetCurrentPrice(product, out int price))
            {
                Debug.LogWarning($"StorePersistence: {product.DisplayName}의 현재 판매 가격이 없어 저장하지 않습니다.", this);
                return false;
            }

            inventoryEntries.Add(new ProductQuantitySaveData
            {
                productId = product.ProductId,
                quantity = inventory.GetQuantity(product)
            });
            priceEntries.Add(new ProductPriceSaveData
            {
                productId = product.ProductId,
                currentPrice = price
            });
        }

        var shelfEntries = new List<ShelfSaveData>(shelves.Length);
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            ProductDefinition assigned = shelf.AssignedProduct;
            string assignedProductId = string.Empty;
            if (assigned != null)
            {
                if (string.IsNullOrWhiteSpace(assigned.ProductId) || !productsById.ContainsKey(assigned.ProductId))
                {
                    Debug.LogWarning($"StorePersistence: {shelf.name}의 진열 상품을 저장 목록에서 찾지 못해 저장하지 않습니다.", this);
                    return false;
                }

                assignedProductId = assigned.ProductId;
            }

            shelfEntries.Add(new ShelfSaveData
            {
                shelfId = shelf.SaveId,
                assignedProductId = assignedProductId,
                currentQuantity = shelf.CurrentQuantity
            });
        }

        var dynamicFacilities = new List<DynamicFacilitySaveData>();
        if (buildMode == null || !buildMode.TryCaptureDynamicFacilities(dynamicFacilities))
        {
            Debug.LogWarning("StorePersistence: 배치된 시설을 저장 목록으로 만들지 못해 저장하지 않습니다.", this);
            return false;
        }

        if (!TryValidateDynamicProductStates(dynamicFacilities, productsById, out string dynamicProductError))
        {
            Debug.LogWarning($"StorePersistence: 배치 시설의 상품 상태를 저장하지 않습니다. {dynamicProductError}", this);
            return false;
        }

        if (upgradeSystem == null)
        {
            Debug.LogWarning("StorePersistence: StoreUpgradeSystem이 연결되지 않아 저장하지 않습니다.", this);
            return false;
        }

        data = new StoreSaveData
        {
            version = SaveVersion,
            day = session.Day,
            currentMoney = economy.CurrentMoney,
            dailyRevenue = economy.DailyRevenue,
            dailyExpense = economy.DailyExpense,
            inventory = inventoryEntries,
            shelves = shelfEntries,
            prices = priceEntries,
            dynamicFacilities = dynamicFacilities,
            checkoutUpgradeLevel = upgradeSystem.GetLevel(StoreUpgradeType.FastCheckout),
            advertisingUpgradeLevel = upgradeSystem.GetLevel(StoreUpgradeType.Advertising),
            trafficUpgradeLevel = upgradeSystem.GetLevel(StoreUpgradeType.WordOfMouth)
        };
        return true;
    }

    bool TryValidateSaveData(StoreSaveData data, out ValidatedSave validated, out string error)
    {
        validated = null;
        error = string.Empty;
        if (data == null)
        {
            error = "저장 데이터 본문이 없습니다.";
            return false;
        }

        if (data.version != LegacySaveVersion
            && data.version != LayoutSaveVersion
            && data.version != ProductSaveVersion
            && data.version != SaveVersion)
        {
            error = $"지원하지 않는 저장 버전입니다. 파일 버전: {data.version}";
            return false;
        }

        if (data.day < 1)
        {
            error = $"Day는 1 이상이어야 합니다. 현재 값: {data.day}";
            return false;
        }

        if (data.currentMoney < 0 || data.dailyRevenue < 0 || data.dailyExpense < 0)
        {
            error = "자금, 매출, 매입 비용은 0 이상이어야 합니다.";
            return false;
        }

        if (!TryBuildProductMap(out Dictionary<string, ProductDefinition> productsById))
        {
            error = "저장 대상 상품 구성이 올바르지 않습니다.";
            return false;
        }

        if (!TryReadQuantities(data.inventory, productsById, "창고", out int[] quantities, out error))
        {
            return false;
        }

        if (!TryReadPrices(data.prices, productsById, out int[] priceValues, out error))
        {
            return false;
        }

        if (!TryReadShelves(data.shelves, productsById, out ProductDefinition[] assignedProducts, out int[] shelfQuantities, out error))
        {
            return false;
        }

        List<DynamicFacilitySaveData> dynamicFacilities = data.version == LegacySaveVersion
            || (data.version == LayoutSaveVersion && data.dynamicFacilities == null)
            ? new List<DynamicFacilitySaveData>()
            : data.dynamicFacilities;
        if (data.version == LayoutSaveVersion && dynamicFacilities != null)
            NormalizeLayoutOnlyProducts(dynamicFacilities);
        if (buildMode == null)
        {
            error = "건설 모드가 연결되지 않았습니다.";
            return false;
        }

        if (!buildMode.TryValidateDynamicLayouts(dynamicFacilities, out error))
        {
            return false;
        }

        if (!TryValidateDynamicProductStates(dynamicFacilities, productsById, out error))
        {
            return false;
        }

        int checkoutUpgradeLevel = 0;
        int advertisingUpgradeLevel = 0;
        int trafficUpgradeLevel = 0;
        if (data.version == SaveVersion)
        {
            if (upgradeSystem == null)
            {
                error = "업그레이드가 연결되지 않았습니다.";
                return false;
            }

            checkoutUpgradeLevel = data.checkoutUpgradeLevel;
            advertisingUpgradeLevel = data.advertisingUpgradeLevel;
            trafficUpgradeLevel = data.trafficUpgradeLevel;
            if (!IsUpgradeLevelValid(StoreUpgradeType.FastCheckout, checkoutUpgradeLevel, out error)
                || !IsUpgradeLevelValid(StoreUpgradeType.Advertising, advertisingUpgradeLevel, out error)
                || !IsUpgradeLevelValid(StoreUpgradeType.WordOfMouth, trafficUpgradeLevel, out error))
            {
                return false;
            }
        }

        validated = new ValidatedSave
        {
            day = data.day,
            currentMoney = data.currentMoney,
            dailyRevenue = data.dailyRevenue,
            dailyExpense = data.dailyExpense,
            inventoryQuantities = quantities,
            prices = priceValues,
            shelfProducts = assignedProducts,
            shelfQuantities = shelfQuantities,
            dynamicFacilities = dynamicFacilities,
            checkoutUpgradeLevel = checkoutUpgradeLevel,
            advertisingUpgradeLevel = advertisingUpgradeLevel,
            trafficUpgradeLevel = trafficUpgradeLevel
        };
        return true;
    }

    bool TryReadQuantities(
        List<ProductQuantitySaveData> entries,
        Dictionary<string, ProductDefinition> productsById,
        string label,
        out int[] quantities,
        out string error)
    {
        quantities = null;
        error = string.Empty;
        if (entries == null || entries.Count != products.Length)
        {
            error = $"{label} 항목 수가 저장 대상 상품 수와 다릅니다.";
            return false;
        }

        quantities = new int[products.Length];
        var seen = new HashSet<string>();
        for (int index = 0; index < entries.Count; index++)
        {
            ProductQuantitySaveData entry = entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.productId) || !productsById.TryGetValue(entry.productId, out ProductDefinition product))
            {
                error = $"{label}에 알 수 없는 상품 Id가 있습니다.";
                return false;
            }

            if (!seen.Add(entry.productId))
            {
                error = $"{label}에 상품 Id {entry.productId}가 중복되어 있습니다.";
                return false;
            }

            if (entry.quantity < 0)
            {
                error = $"{label} 수량은 0 이상이어야 합니다. 상품: {entry.productId}";
                return false;
            }

            quantities[IndexOfProduct(product)] = entry.quantity;
        }

        if (seen.Count != products.Length)
        {
            error = $"{label}에 저장 대상 상품이 모두 포함되어 있지 않습니다.";
            return false;
        }

        return true;
    }

    bool TryReadPrices(
        List<ProductPriceSaveData> entries,
        Dictionary<string, ProductDefinition> productsById,
        out int[] priceValues,
        out string error)
    {
        priceValues = null;
        error = string.Empty;
        if (entries == null || entries.Count != products.Length)
        {
            error = "가격 항목 수가 저장 대상 상품 수와 다릅니다.";
            return false;
        }

        priceValues = new int[products.Length];
        var seen = new HashSet<string>();
        for (int index = 0; index < entries.Count; index++)
        {
            ProductPriceSaveData entry = entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.productId) || !productsById.TryGetValue(entry.productId, out ProductDefinition product))
            {
                error = "가격에 알 수 없는 상품 Id가 있습니다.";
                return false;
            }

            if (!seen.Add(entry.productId))
            {
                error = $"가격에 상품 Id {entry.productId}가 중복되어 있습니다.";
                return false;
            }

            if (entry.currentPrice < 0)
            {
                error = $"판매 가격은 0 이상이어야 합니다. 상품: {entry.productId}";
                return false;
            }

            if (!pricing.TryGetCurrentPrice(product, out _))
            {
                error = $"{product.DisplayName}의 현재 판매 가격 목록이 없습니다.";
                return false;
            }

            priceValues[IndexOfProduct(product)] = entry.currentPrice;
        }

        if (seen.Count != products.Length)
        {
            error = "가격에 저장 대상 상품이 모두 포함되어 있지 않습니다.";
            return false;
        }

        return true;
    }

    bool TryReadShelves(
        List<ShelfSaveData> entries,
        Dictionary<string, ProductDefinition> productsById,
        out ProductDefinition[] assignedProducts,
        out int[] quantities,
        out string error)
    {
        assignedProducts = null;
        quantities = null;
        error = string.Empty;
        if (!TryBuildShelfMap(out Dictionary<string, int> shelfIndexes))
        {
            error = "진열 시설 구성이 올바르지 않습니다.";
            return false;
        }

        if (entries == null || entries.Count != shelves.Length)
        {
            error = "진열 항목 수가 저장 대상 진열 시설 수와 다릅니다.";
            return false;
        }

        assignedProducts = new ProductDefinition[shelves.Length];
        quantities = new int[shelves.Length];
        var seen = new HashSet<string>();
        for (int index = 0; index < entries.Count; index++)
        {
            ShelfSaveData entry = entries[index];
            if (entry == null || string.IsNullOrWhiteSpace(entry.shelfId) || !shelfIndexes.TryGetValue(entry.shelfId, out int shelfIndex))
            {
                error = "알 수 없는 진열 시설 Id가 있습니다.";
                return false;
            }

            if (!seen.Add(entry.shelfId))
            {
                error = $"진열 시설 Id {entry.shelfId}가 중복되어 있습니다.";
                return false;
            }

            Shelf shelf = shelves[shelfIndex];
            if (string.IsNullOrEmpty(entry.assignedProductId))
            {
                if (entry.currentQuantity != 0)
                {
                    error = $"{entry.shelfId}에 진열 상품이 없으면 수량은 0이어야 합니다.";
                    return false;
                }

                assignedProducts[shelfIndex] = null;
                quantities[shelfIndex] = 0;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.assignedProductId) || !productsById.TryGetValue(entry.assignedProductId, out ProductDefinition product))
            {
                error = $"{entry.shelfId}의 진열 상품 Id를 찾지 못했습니다.";
                return false;
            }

            if (product.StorageType != shelf.AcceptedStorageType)
            {
                error = $"{entry.shelfId}은 {product.DisplayName}의 보관 타입을 받을 수 없습니다.";
                return false;
            }

            if (entry.currentQuantity < 0 || entry.currentQuantity > product.MaxShelfCount)
            {
                error = $"{entry.shelfId}의 수량이 허용 범위를 벗어났습니다. 수량: {entry.currentQuantity}";
                return false;
            }

            assignedProducts[shelfIndex] = product;
            quantities[shelfIndex] = entry.currentQuantity;
        }

        if (seen.Count != shelves.Length)
        {
            error = "저장 대상 진열 시설이 모두 포함되어 있지 않습니다.";
            return false;
        }

        return true;
    }

    bool ApplyValidatedSave(ValidatedSave validated)
    {
        if (!economy.TryRestoreState(validated.currentMoney, validated.dailyRevenue, validated.dailyExpense))
        {
            Debug.LogError("StorePersistence: 경제 상태 복원에 실패했습니다.", this);
            return false;
        }

        if (upgradeSystem == null
            || !upgradeSystem.TryRestoreLevels(
                validated.checkoutUpgradeLevel,
                validated.advertisingUpgradeLevel,
                validated.trafficUpgradeLevel))
        {
            Debug.LogError("StorePersistence: 업그레이드 복원에 실패했습니다.", this);
            return false;
        }

        for (int index = 0; index < products.Length; index++)
        {
            if (!inventory.TryRestoreQuantity(products[index], validated.inventoryQuantities[index]))
            {
                Debug.LogError("StorePersistence: 창고 수량 복원에 실패했습니다.", this);
                return false;
            }
        }

        for (int index = 0; index < products.Length; index++)
        {
            if (!pricing.TrySetPrice(products[index], validated.prices[index]))
            {
                Debug.LogError("StorePersistence: 판매 가격 복원에 실패했습니다.", this);
                return false;
            }
        }

        for (int index = 0; index < shelves.Length; index++)
        {
            if (!shelves[index].TryRestoreState(validated.shelfProducts[index], validated.shelfQuantities[index]))
            {
                Debug.LogError("StorePersistence: 진열 상태 복원에 실패했습니다.", this);
                return false;
            }
        }

        if (!session.TryRestorePreparationState(validated.day))
        {
            Debug.LogError("StorePersistence: Day 복원에 실패했습니다.", this);
            return false;
        }

        if (buildMode == null || !buildMode.TryReplaceDynamicLayouts(validated.dynamicFacilities, products))
        {
            Debug.LogError("StorePersistence: 배치 시설 복원에 실패했습니다.", this);
            return false;
        }

        statistics.ResetDailyStatistics();
        return true;
    }

    static void NormalizeLayoutOnlyProducts(List<DynamicFacilitySaveData> records)
    {
        for (int index = 0; index < records.Count; index++)
        {
            DynamicFacilitySaveData record = records[index];
            if (record == null)
                continue;

            record.productId = string.Empty;
            record.quantity = 0;
        }
    }

    bool TryValidateDynamicProductStates(
        List<DynamicFacilitySaveData> records,
        Dictionary<string, ProductDefinition> productsById,
        out string error)
    {
        error = string.Empty;
        if (records == null)
        {
            error = "동적 시설 목록이 없습니다.";
            return false;
        }

        for (int index = 0; index < records.Count; index++)
        {
            DynamicFacilitySaveData record = records[index];
            if (record == null || !buildMode.TryGetFacilityDefinition(record.facilityId, out FacilityDefinition definition))
            {
                error = "동적 시설의 상품 상태를 확인할 수 없습니다.";
                return false;
            }

            string productId = string.IsNullOrWhiteSpace(record.productId) ? string.Empty : record.productId;
            record.productId = productId;
            if (definition.FacilityType == FacilityType.Checkout)
            {
                if (productId.Length > 0 || record.quantity != 0)
                {
                    error = $"{definition.FacilityId}에는 상품 상태를 저장할 수 없습니다.";
                    return false;
                }

                continue;
            }

            if (!definition.TryGetAcceptedStorageType(out ProductStorageType storageType))
            {
                error = $"{definition.FacilityId}의 보관 타입을 확인하지 못했습니다.";
                return false;
            }

            if (productId.Length == 0)
            {
                if (record.quantity != 0)
                {
                    error = $"{definition.FacilityId}의 상품이 없으면 수량은 0이어야 합니다.";
                    return false;
                }

                continue;
            }

            if (!productsById.TryGetValue(productId, out ProductDefinition product))
            {
                error = $"{definition.FacilityId}의 상품 Id {productId}를 찾지 못했습니다.";
                return false;
            }

            if (product.StorageType != storageType)
            {
                error = $"{definition.FacilityId}에 {product.DisplayName}을 진열할 수 없습니다.";
                return false;
            }

            if (record.quantity < 0 || record.quantity > product.MaxShelfCount)
            {
                error = $"{definition.FacilityId}의 수량 {record.quantity}이 허용 범위를 벗어났습니다.";
                return false;
            }
        }

        return true;
    }

    bool TryValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (!TryBuildProductMap(out _))
        {
            error = "상품 Id가 비어 있거나 중복되어 있습니다.";
            return false;
        }

        if (!TryBuildShelfMap(out _))
        {
            error = "진열 시설 Id가 비어 있거나 중복되어 있습니다.";
            return false;
        }

        if (buildMode == null)
        {
            error = "건설 모드가 연결되지 않았습니다.";
            return false;
        }

        return true;
    }

    bool TryBuildProductMap(out Dictionary<string, ProductDefinition> productsById)
    {
        productsById = new Dictionary<string, ProductDefinition>();
        if (products == null || products.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < products.Length; index++)
        {
            ProductDefinition product = products[index];
            if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
            {
                return false;
            }

            if (productsById.ContainsKey(product.ProductId))
            {
                return false;
            }

            productsById.Add(product.ProductId, product);
        }

        return true;
    }

    bool TryBuildShelfMap(out Dictionary<string, int> shelfIndexes)
    {
        shelfIndexes = new Dictionary<string, int>();
        if (shelves == null || shelves.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null || string.IsNullOrWhiteSpace(shelf.SaveId))
            {
                return false;
            }

            if (shelfIndexes.ContainsKey(shelf.SaveId))
            {
                return false;
            }

            shelfIndexes.Add(shelf.SaveId, index);
        }

        return true;
    }

    int IndexOfProduct(ProductDefinition product)
    {
        for (int index = 0; index < products.Length; index++)
        {
            if (products[index] == product)
            {
                return index;
            }
        }

        return -1;
    }

    void OnValidate()
    {
        if (session == null)
        {
            Debug.LogWarning("StorePersistence: StoreSession이 연결되지 않았습니다.", this);
        }

        if (economy == null)
        {
            Debug.LogWarning("StorePersistence: StoreEconomy가 연결되지 않았습니다.", this);
        }

        if (inventory == null)
        {
            Debug.LogWarning("StorePersistence: StoreInventory가 연결되지 않았습니다.", this);
        }

        if (pricing == null)
        {
            Debug.LogWarning("StorePersistence: StorePricing이 연결되지 않았습니다.", this);
        }

        if (statistics == null)
        {
            Debug.LogWarning("StorePersistence: StoreStatistics가 연결되지 않았습니다.", this);
        }

        if (buildMode == null)
        {
            Debug.LogWarning("StorePersistence: BuildModeController가 연결되지 않았습니다.", this);
        }

        if (products == null || products.Length == 0)
        {
            Debug.LogWarning("StorePersistence: 저장할 상품이 없습니다.", this);
        }
        else
        {
            var seenProducts = new HashSet<string>();
            for (int index = 0; index < products.Length; index++)
            {
                ProductDefinition product = products[index];
                if (product == null || string.IsNullOrWhiteSpace(product.ProductId))
                {
                    Debug.LogWarning($"StorePersistence: 저장 상품 {index}의 Product Id가 비어 있습니다.", this);
                    continue;
                }

                if (!seenProducts.Add(product.ProductId))
                {
                    Debug.LogWarning($"StorePersistence: Product Id {product.ProductId}가 중복되어 있습니다.", this);
                }
            }
        }

        if (shelves == null || shelves.Length == 0)
        {
            Debug.LogWarning("StorePersistence: 저장할 진열 시설이 없습니다.", this);
            return;
        }

        var seenShelves = new HashSet<string>();
        for (int index = 0; index < shelves.Length; index++)
        {
            Shelf shelf = shelves[index];
            if (shelf == null || string.IsNullOrWhiteSpace(shelf.SaveId))
            {
                Debug.LogWarning($"StorePersistence: 진열 시설 {index}의 Save Id가 비어 있습니다.", this);
                continue;
            }

            if (!seenShelves.Add(shelf.SaveId))
            {
                Debug.LogWarning($"StorePersistence: Save Id {shelf.SaveId}가 중복되어 있습니다.", this);
            }
        }
    }

    [Serializable]
    public class StoreSaveData
    {
        public int version;
        public int day;
        public int currentMoney;
        public int dailyRevenue;
        public int dailyExpense;
        public List<ProductQuantitySaveData> inventory;
        public List<ShelfSaveData> shelves;
        public List<ProductPriceSaveData> prices;
        public List<DynamicFacilitySaveData> dynamicFacilities;
        public int checkoutUpgradeLevel;
        public int advertisingUpgradeLevel;
        public int trafficUpgradeLevel;
    }

    [Serializable]
    public class DynamicFacilitySaveData
    {
        public string facilityId;
        public int gridX;
        public int gridY;
        public int rotationQuarterTurns;
        public string productId;
        public int quantity;
    }

    [Serializable]
    public class ProductQuantitySaveData
    {
        public string productId;
        public int quantity;
    }

    [Serializable]
    public class ProductPriceSaveData
    {
        public string productId;
        public int currentPrice;
    }

    [Serializable]
    public class ShelfSaveData
    {
        public string shelfId;
        public string assignedProductId;
        public int currentQuantity;
    }

    sealed class ValidatedSave
    {
        public int day;
        public int currentMoney;
        public int dailyRevenue;
        public int dailyExpense;
        public int[] inventoryQuantities;
        public int[] prices;
        public ProductDefinition[] shelfProducts;
        public int[] shelfQuantities;
        public List<DynamicFacilitySaveData> dynamicFacilities;
        public int checkoutUpgradeLevel;
        public int advertisingUpgradeLevel;
        public int trafficUpgradeLevel;
    }

    bool IsUpgradeLevelValid(StoreUpgradeType type, int level, out string error)
    {
        int maxLevel = upgradeSystem.GetMaxLevel(type);
        if (level < 0 || level > maxLevel)
        {
            error = $"{upgradeSystem.GetDisplayName(type)} 레벨이 올바르지 않습니다. 현재 값: {level}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
