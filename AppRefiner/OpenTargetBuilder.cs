using AppRefiner.Database;
using AppRefiner.Database.Models;
using System;
using System.Collections.Generic;

namespace AppRefiner
{
    /// <summary>
    /// Utility for creating OpenTarget instances from editor captions
    /// </summary>
    public static class OpenTargetBuilder
    {
        /// <summary>
        /// Creates an OpenTarget from an editor caption
        /// </summary>
        /// <param name="caption">The editor caption (e.g., "RECORD.name.FIELD.name.EVENT.FieldChange (Record PeopleCode)")</param>
        /// <returns>OpenTarget or null if caption cannot be parsed</returns>
        public static OpenTarget? CreateFromCaption(string? caption)
        {
            if (string.IsNullOrEmpty(caption))
                return null;

            try
            {
                // Extract the type suffix (text in parentheses at the end)
                int typeStartIndex = caption.LastIndexOf('(');
                int typeEndIndex = caption.LastIndexOf(')');

                if (typeStartIndex < 0 || typeEndIndex <= typeStartIndex)
                    return null;

                string editorType = caption.Substring(typeStartIndex + 1, typeEndIndex - typeStartIndex - 1);
                string captionWithoutType = caption.Substring(0, typeStartIndex).Trim();

                // Parse based on editor type
                // TODO: User will implement each case by capturing example captions
                switch (editorType)
                {
                    case "Record PeopleCode":
                        return ParseRecordPeopleCode(captionWithoutType);

                    case "Component PeopleCode":
                        return ParseComponentPeopleCode(captionWithoutType);

                    case "Page PeopleCode":
                        return ParsePagePeopleCode(captionWithoutType);

                    case "Application Package PeopleCode":
                        return ParseApplicationPackagePeopleCode(captionWithoutType);

                    case "App Engine Program PeopleCode":
                        return ParseAppEnginePeopleCode(captionWithoutType);

                    case "Menu PeopleCode":
                        return ParseMenuPeopleCode(captionWithoutType);

                    case "Message PeopleCode":
                        return ParseMessagePeopleCode(captionWithoutType);

                    case "Component Interface PeopleCode":
                        return ParseComponentInterfacePeopleCode(captionWithoutType);

                    default:
                        Debug.Log($"OpenTargetBuilder: Unknown editor type '{editorType}'");
                        return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, "OpenTargetBuilder.CreateFromCaption");
                return null;
            }
        }

        /// <summary>
        /// Parses Record PeopleCode caption
        /// Example: "PSOPRDEFN.USERIDALIAS.SaveEdit" → RECORD.PSOPRDEFN / FIELD.USERIDALIAS / METHOD.SaveEdit
        /// </summary>
        private static OpenTarget? ParseRecordPeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: RecordName.FieldName.EventName
            var parts = captionWithoutType.Split('.');

            // Need exactly 3 parts: Record.Field.Event
            if (parts.Length != 3)
            {
                Debug.Log($"ParseRecordPeopleCode: Invalid format, expected 3 parts - '{captionWithoutType}'");
                return null;
            }

            string recordName = parts[0];
            string fieldName = parts[1];
            string eventName = parts[2];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.RECORD, recordName),
                (PSCLASSID.FIELD, fieldName),
                (PSCLASSID.METHOD, eventName)
            };

            return new OpenTarget(
                OpenTargetType.RecordFieldPeopleCode,
                $"{recordName}.{fieldName}",
                $"Record PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses Component PeopleCode caption
        /// Example formats:
        /// - "COMPONENT_NAME.MARKET_VALUE.EVENT_NAME" (Component level - 3 parts)
        /// - "COMPONENT.MARKET.RECORD.EVENT" (Component Record level - 4 parts)
        /// - "COMPONENT.MARKET.RECORD.FIELD.EVENT" (Component Record Field level - 5 parts)
        /// </summary>
        private static OpenTarget? ParseComponentPeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots
            var parts = captionWithoutType.Split('.');

            // Component level: COMPONENT.MARKET.METHOD (3 parts)
            if (parts.Length == 3)
            {
                string componentName = parts[0];
                string market = parts[1];
                string method = parts[2];

                List<(PSCLASSID, string)> objectPairs = new()
                {
                    (PSCLASSID.COMPONENT, componentName),
                    (PSCLASSID.MARKET, market),
                    (PSCLASSID.METHOD, method)
                };

                return new OpenTarget(
                    OpenTargetType.ComponentPeopleCode,
                    componentName,
                    $"Component PeopleCode: {captionWithoutType}",
                    objectPairs
                );
            }
            // Component Record level: COMPONENT.MARKET.RECORD.METHOD (4 parts)
            else if (parts.Length == 4)
            {
                string componentName = parts[0];
                string market = parts[1];
                string recordName = parts[2];
                string method = parts[3];

                List<(PSCLASSID, string)> objectPairs = new()
                {
                    (PSCLASSID.COMPONENT, componentName),
                    (PSCLASSID.MARKET, market),
                    (PSCLASSID.RECORD, recordName),
                    (PSCLASSID.METHOD, method)
                };

                return new OpenTarget(
                    OpenTargetType.ComponentRecordPeopleCode,
                    $"{componentName}.{recordName}",
                    $"Component Record PeopleCode: {captionWithoutType}",
                    objectPairs
                );
            }
            // Component Record Field level: COMPONENT.MARKET.RECORD.FIELD.METHOD (5 parts)
            else if (parts.Length == 5)
            {
                string componentName = parts[0];
                string market = parts[1];
                string recordName = parts[2];
                string fieldName = parts[3];
                string method = parts[4];

                List<(PSCLASSID, string)> objectPairs = new()
                {
                    (PSCLASSID.COMPONENT, componentName),
                    (PSCLASSID.MARKET, market),
                    (PSCLASSID.RECORD, recordName),
                    (PSCLASSID.FIELD, fieldName),
                    (PSCLASSID.METHOD, method)
                };

                return new OpenTarget(
                    OpenTargetType.ComponentRecFieldPeopleCode,
                    $"{componentName}.{recordName}.{fieldName}",
                    $"Component Record Field PeopleCode: {captionWithoutType}",
                    objectPairs
                );
            }
            else
            {
                Debug.Log($"ParseComponentPeopleCode: Invalid format, expected 3, 4, or 5 parts - '{captionWithoutType}'");
                return null;
            }
        }

        /// <summary>
        /// Parses Page PeopleCode caption
        /// Example: "OPRROWS.Activate" → PAGE.OPRROWS / METHOD.Activate
        /// </summary>
        private static OpenTarget? ParsePagePeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: PageName.EventName
            var parts = captionWithoutType.Split('.');

            // Need exactly 2 parts: Page.Event
            if (parts.Length != 2)
            {
                Debug.Log($"ParsePagePeopleCode: Invalid format, expected 2 parts - '{captionWithoutType}'");
                return null;
            }

            string pageName = parts[0];
            string eventName = parts[1];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.PAGE, pageName),
                (PSCLASSID.METHOD, eventName)
            };

            return new OpenTarget(
                OpenTargetType.PagePeopleCode,
                pageName,
                $"Page PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses Application Package PeopleCode caption
        /// Examples:
        /// - "ADS.Common.OnExecute" → Package(104).ADS / Class.Common / Method.OnExecute
        /// - "ADS.Relation.CriteriaUI.OnExecute" → Package(104).ADS / Package1(105).Relation / Class.CriteriaUI / Method.OnExecute
        /// </summary>
        private static OpenTarget? ParseApplicationPackagePeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: Package1[.Package2[.Package3]].ClassName.MethodName
            var parts = captionWithoutType.Split('.');

            // Need at least 3 parts: Package.Class.Method
            if (parts.Length < 3)
            {
                Debug.Log($"ParseApplicationPackagePeopleCode: Invalid format, need at least 3 parts - '{captionWithoutType}'");
                return null;
            }

            // Last part is the method name (usually "OnExecute")
            string methodName = parts[^1];

            // Second-to-last part is the class name
            string className = parts[^2];

            // Everything before that is package levels (1-3 levels allowed)
            int packageCount = parts.Length - 2;
            if (packageCount > 3)
            {
                Debug.Log($"ParseApplicationPackagePeopleCode: Too many package levels ({packageCount}), max is 3 - '{captionWithoutType}'");
                return null;
            }

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new();

            // Add packages starting at PSCLASSID 104 (APPLICATION_PACKAGE)
            int packageClassId = 104;
            for (int i = 0; i < packageCount; i++)
            {
                objectPairs.Add(((PSCLASSID)packageClassId++, parts[i]));
            }

            // Add class name
            objectPairs.Add((PSCLASSID.APPLICATION_CLASS, className));

            // Add method name
            objectPairs.Add((PSCLASSID.METHOD, methodName));

            return new OpenTarget(
                OpenTargetType.ApplicationClass,
                className,
                $"Application Package: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses App Engine PeopleCode caption
        /// Example: "3CENGINE.MAIN.GBL.default.1900-01-01.M04.OnExecute"
        /// Parts: AEAPPLICATIONID.AESECTION.MARKET.DBTYPE.EFFDT.AESTEP.METHOD
        /// </summary>
        private static OpenTarget? ParseAppEnginePeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: AEApplicationID.AESection.Market.DBType.EffDt.AEStep.Method
            var parts = captionWithoutType.Split('.');

            // Need exactly 7 parts
            if (parts.Length != 7)
            {
                Debug.Log($"ParseAppEnginePeopleCode: Invalid format, expected 7 parts - '{captionWithoutType}'");
                return null;
            }

            string aeApplicationId = parts[0];
            string aeSection = parts[1];
            string market = parts[2];
            string dbType = parts[3];
            string effDt = parts[4];
            string aeStep = parts[5];
            string method = parts[6];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.AEAPPLICATIONID, aeApplicationId),
                (PSCLASSID.AESECTION, aeSection),
                (PSCLASSID.MARKET, market),
                (PSCLASSID.DBTYPE, dbType),
                (PSCLASSID.EFFDT, effDt),
                (PSCLASSID.AESTEP, aeStep),
                (PSCLASSID.METHOD, method)
            };

            return new OpenTarget(
                OpenTargetType.AppEnginePeopleCode,
                aeApplicationId,
                $"App Engine PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses Menu PeopleCode caption
        /// Example (assumed): "MENU_NAME.EVENT_NAME" → MENU.MENU_NAME / METHOD.EVENT_NAME
        /// </summary>
        private static OpenTarget? ParseMenuPeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: MenuName.EventName
            var parts = captionWithoutType.Split('.');

            // Need exactly 2 parts: Menu.Event
            if (parts.Length != 2)
            {
                Debug.Log($"ParseMenuPeopleCode: Invalid format, expected 2 parts - '{captionWithoutType}'");
                return null;
            }

            string menuName = parts[0];
            string eventName = parts[1];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.MENU, menuName),
                (PSCLASSID.METHOD, eventName)
            };

            return new OpenTarget(
                OpenTargetType.MenuPeopleCode,
                menuName,
                $"Menu PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses Message PeopleCode caption
        /// Example: "ACCOUNT_CHARTFIELD_SYNC.OnRouteSend" → MESSAGE.ACCOUNT_CHARTFIELD_SYNC / METHOD.OnRouteSend
        /// </summary>
        private static OpenTarget? ParseMessagePeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: MessageName.EventName
            var parts = captionWithoutType.Split('.');

            // Need exactly 2 parts: Message.Event
            if (parts.Length != 2)
            {
                Debug.Log($"ParseMessagePeopleCode: Invalid format, expected 2 parts - '{captionWithoutType}'");
                return null;
            }

            string messageName = parts[0];
            string eventName = parts[1];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.MESSAGE, messageName),
                (PSCLASSID.METHOD, eventName)
            };

            return new OpenTarget(
                OpenTargetType.MessagePeopleCode,
                messageName,
                $"Message PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }

        /// <summary>
        /// Parses Component Interface PeopleCode caption
        /// Example: "ACAD_TST_RSLT_PERS.Methods" → COMPONENTINTERFACE.ACAD_TST_RSLT_PERS / METHOD.Methods
        /// </summary>
        private static OpenTarget? ParseComponentInterfacePeopleCode(string captionWithoutType)
        {
            if (string.IsNullOrEmpty(captionWithoutType))
                return null;

            // Split by dots: ComponentInterfaceName.EventName
            var parts = captionWithoutType.Split('.');

            // Need exactly 2 parts: ComponentInterface.Event
            if (parts.Length != 2)
            {
                Debug.Log($"ParseComponentInterfacePeopleCode: Invalid format, expected 2 parts - '{captionWithoutType}'");
                return null;
            }

            string componentInterfaceName = parts[0];
            string eventName = parts[1];

            // Build the object pairs
            List<(PSCLASSID, string)> objectPairs = new()
            {
                (PSCLASSID.COMPONENTINTERFACE, componentInterfaceName),
                (PSCLASSID.METHOD, eventName)
            };

            return new OpenTarget(
                OpenTargetType.ComponentInterfacePeopleCode,
                componentInterfaceName,
                $"Component Interface PeopleCode: {captionWithoutType}",
                objectPairs
            );
        }
    }
}
