using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MotorPassport
{
    public string product_id;
    public string source_site;
    public string asset_type;
    public float rated_power_kw;
    public int operating_hours;
    public int installation_year;
    public string[] failure_symptoms;
    public string[] location_context;
    public PassportComponent[] components;
}

[Serializable]
public class PassportComponent
{
    public string component_id;
    public string part_id;
    public string name;
    public string material;
    public float health_score;
    public float risk_score;
    public float confidence;
    public string damage_type;
    public string test_required;
    public float co2_saving_kg;
    public float recovered_value_aed;
    public string decision;
    public string robot_action;
    public string target_station;
}

public class DigitalPassportLoader : MonoBehaviour
{
    public TextAsset passportJson;
    public MotorPassport Passport { get; private set; }

    private readonly Dictionary<string, PassportComponent> componentsById = new Dictionary<string, PassportComponent>();

    private void Awake()
    {
        LoadPassport();
    }

    public void LoadPassport()
    {
        if (passportJson == null)
        {
            Debug.LogWarning("No passport JSON assigned.");
            return;
        }

        Passport = UnityEngine.JsonUtility.FromJson<MotorPassport>(passportJson.text);
        componentsById.Clear();
        if (Passport != null && Passport.components != null)
        {
            foreach (PassportComponent component in Passport.components)
            {
                componentsById[component.component_id] = component;
            }
        }
    }

    public PassportComponent GetComponentData(string componentId)
    {
        PassportComponent component;
        componentsById.TryGetValue(componentId, out component);
        return component;
    }

    public string GetDecision(string componentId)
    {
        PassportComponent component = GetComponentData(componentId);
        return component == null ? "unknown" : component.decision;
    }
}
