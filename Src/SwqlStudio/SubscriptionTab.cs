﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SolarWinds.InformationService.Contract2;

namespace SwqlStudio
{
    public partial class SubscriptionTab : UserControl
    {
        public SubscriptionTab()
        {
            InitializeComponent();
        }

        internal void AddIndication(IndicationEventArgs e)
        {
            //Create the root notification node.
            string rootDisplayName = GetNotificationDisplayName(e.IndicationType);

            int imageIndex = DetermineImageIndex(e.IndicationType);
            var notificationRootNode = new TreeNode(rootDisplayName, imageIndex, imageIndex);

            if (e.IndicationProperties != null && e.IndicationProperties.Count > 0)
            {
                //Create the IndicationProperties and add it to the root.
                var indicationPropertiesNode = new TreeNode("Indication Properties", -1, -1);
                notificationRootNode.Nodes.Add(indicationPropertiesNode);

                //Add all the properties.
                AddProperties(indicationPropertiesNode, e.IndicationProperties);
                indicationPropertiesNode.Expand();
            }

            if (e.SourceInstanceProperties != null && e.SourceInstanceProperties.Count > 0)
            {
                //Create the IndicationProperties and add it to the root.
                var instancePropertiesNode = new TreeNode("Source Instance Properties");
                notificationRootNode.Nodes.Add(instancePropertiesNode);

                //Add all the properties.
                AddProperties(instancePropertiesNode, e.SourceInstanceProperties);
                instancePropertiesNode.Expand();
            }


            //_notifications.Add(indicationId, notificationRootNode);

            //Finally, add all these nodes to the tree.
            NotificationsTreeView.Nodes.Add(notificationRootNode);
        }

        private static string GetNotificationDisplayName(string indicationType)
        {
            return string.Format("{0} - {1} {2}", indicationType, DateTime.Now.ToShortDateString(),
                                 DateTime.Now.ToShortTimeString());
        }

        private int DetermineImageIndex(string indicationType)
        {
            switch (indicationType)
            {
                case "System.InstanceCreated":
                    return 1;
                case "System.InstanceModified":
                    return 2;
                case "System.InstanceDeleted":
                    return 3;
                default:
                    return 4;
            }
        }

        private static void AddProperties(TreeNode parent, PropertyBag properties)
        {
            //Add all the properties.
            foreach (KeyValuePair<string, object> property in properties)
            {
                if (property.Value is PropertyBag)
                {
                    var bagNode = new TreeNode(property.Key);
                    parent.Nodes.Add(bagNode);
                    AddProperties(bagNode, (PropertyBag)property.Value);
                    bagNode.Expand();
                }
                else
                {
                    string propertyDisplayName = string.Format("{0}:{1}", property.Key, property.Value ?? "{null}");
                    var propertyNode = new TreeNode(propertyDisplayName);

                    parent.Nodes.Add(propertyNode);
                }
            }
        }
    }
}
