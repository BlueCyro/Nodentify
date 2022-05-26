using HarmonyLib;
using NeosModLoader;
using FrooxEngine;
using FrooxEngine.LogiX;
using FrooxEngine.UIX;
using BaseX;



namespace Nodentify;
public class Nodentify : NeosMod
{
    public override string Author => "Cyro";
    public override string Name => "Nodentify";
    public override string Version => "1.0.2";

    private static ModConfiguration? Config;

    [AutoRegisterConfigKey]
    private static ModConfigurationKey<double> PressDelay = new ModConfigurationKey<double>("PressDelay", "Press delay for node actions", () => 0.25);

    static void press(IButton b, ButtonEventData d) { _then = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime; }

    static void hold(IButton b, ButtonEventData d, TextEditor e)
    {
        if (e.IsDestroyed || flag)
            return;
        _lastTime = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime - _then;
        if (_lastTime > Config!.GetValue<double>(PressDelay))
        {
            flag = true;
            e.Focus();
        }
    }

    static void refHold(IButton b, ButtonEventData d, IReferenceNode __instance)
    {
        if (flag)
            return;
        _lastTime = Engine.Current.WorldManager.FocusedWorld.Time.WorldTime - _then;
        if (_lastTime > Config!.GetValue<double>(PressDelay))
        {
            flag = true;
            InspectorHelper.OpenInspectorForTarget(__instance.Target ?? __instance, null, __instance.Target != null);
        }
    }

    static void release(IButton b, ButtonEventData d)
    {
        _lastTime = 0.0;
        flag = false;
    }
    static double _lastTime;
    static bool flag = false;
    static double _then;
    public override void OnEngineInit()
    {
        Harmony harmony = new Harmony("net.Cyro.Nodentify");
        Config = GetConfiguration();
        Config!.Save(true);
        harmony.PatchAll();
    }


    [HarmonyPatch(typeof(LogixNode), "GenerateUI")]
    static class LogixNode_GenerateUI_Patch
    {
        static void Postfix(LogixNode __instance, UIBuilder __result, Slot root, float minWidth = 0f, float minHeight = 0f)
        {
            if (__result.Current == null)
                return;

            Text? t =  __result.Current.GetComponent<Text>();
            if (t == null)
                return;
            Slot textSlot = t.Slot;
            Slot instanceSlot = __instance.Slot;
            instanceSlot.Tag = instanceSlot.Tag == null || instanceSlot.Tag.Length == 0 ? null : instanceSlot.Tag;
            string? tagLabel = instanceSlot.Tag;
            
            string originalText = t.Content.Value;
            t.NullContent.Value = originalText;
            ISyncMember tagMember = instanceSlot.GetSyncMember("Tag");
            t.Content.DriveFrom((IField<string>)tagMember, true);
            textSlot.Tag = "Nodentify.Node.AlteredText";
            
            TextEditor textEditor = textSlot.AttachComponent<TextEditor>();
            textEditor.Text.Target = t;
            textEditor.FinishHandling.Value = TextEditor.FinishAction.NullOnEmpty;
            Button b = textSlot.AttachComponent<Button>();
            b.LocalPressed += press;
            b.LocalPressing += (IButton b, ButtonEventData d) => hold(b, d, textEditor);
            b.LocalReleased += release;
        }
    }

    [HarmonyPatch(typeof(ReferenceNode<IChangeable>), "OnGenerateVisual")]
    static class ReferenceNode_OnGenerateVisual_Patch
    {
        static void Postfix(IReferenceNode __instance, Slot root)
        {
            Slot? canvas = root.FindChild((s) => s.Name == "Canvas");
            if (canvas == null)
                return;
            canvas.Tag = "Nodentify.Node.AlteredRef";
            Button b = canvas.AttachComponent<Button>();
            b.LocalPressed += press;
            b.LocalPressing += (IButton b, ButtonEventData d) => refHold(b, d, __instance);
            b.LocalReleased += release;
            color col = __instance.GetType().GetGenericArguments()[0].GetColor().SetA(0.8f);
            canvas[0].GetComponent<Image>().Tint.Value = col;
            canvas[0][0].GetComponent<Image>().Tint.Value = col;
        }
    }
    
    [HarmonyPatch(typeof(ComponentBase<Component>), "OnStart")]
    static class ComponentBase_OnStart_Patch
    {
        static void Postfix(Component __instance)
        {
            if (!(__instance as LogixNode)?.HasVisual == true)
                return;
            
            if (__instance is IReferenceNode)
            {
                Slot? canvas = __instance.Slot.FindChild((s) => s.Tag == "Nodentify.Node.AlteredRef");
                if (canvas == null)
                    return;
                
                IReferenceNode? reference = __instance as IReferenceNode;
                Button refButton = canvas.GetComponent<Button>();
                refButton.LocalPressed += press;
                refButton.LocalPressing += (IButton b, ButtonEventData d) => refHold(b, d, reference!);
                refButton.LocalReleased += release;
                return;
            }
            Slot s = __instance.Slot.FindChild((s) => s.Tag == "Nodentify.Node.AlteredText", 25);
            if (s == null)
                return;

            Button b = s.GetComponent<Button>();
            TextEditor textEditor = s.GetComponent<TextEditor>();

            if(b == null || textEditor == null)
                return;
            
            b.LocalPressed += press;
            b.LocalPressing += (IButton b, ButtonEventData d) => hold(b, d, textEditor);
            b.LocalReleased += release;
        }
    }
}
