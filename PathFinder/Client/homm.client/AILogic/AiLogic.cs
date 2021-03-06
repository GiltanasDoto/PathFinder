﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Threading.Tasks;
using HoMM;
using Homm.Client.Actions;
using Homm.Client.Helpers;
using Homm.Client.Interfaces;
using HoMM.ClientClasses;
using HommFinder;

namespace Homm.Client.AILogic
{
	public abstract class AiLogic 
	{
		public HommSensorData SensorData { get; set; }
		public HommClient Client { get; set; }
		public List<Cell> Map { get; set; }
		public Cell CurrentCell { get; set; }
		public Finder Finder { get; set; }



		//TODO:Need to call this function every day if playing vs player, or you don't see whole map
		public void UpdateMap()
		{
			Map = SensorData.Map.Objects.Select(item => item.ToCell(SensorData.MyArmy)).ToList();
			CurrentCell = SensorData.Location.CreateCell();
			
			Finder = new Finder(Map, CurrentCell);
		}

		protected AiLogic(HommClient client, HommSensorData sensorData)
		{
			SensorData = sensorData;
			Client = client;
			UpdateMap();
		}

		protected List<Cell> workingWithMines()
		{
			var path = new List<Cell>();
			var startCell = SensorData.Location.CreateCell();
			var availableMines = searchAvailableMines(Finder.Cells);
			for (int i = 0; i < availableMines.Count; i++)
			{
				if (i > 1)
				{
					startCell = path.LastOrDefault();
				}
				path.AddRange(Finder.GetSmartPath(startCell, (availableMines[i])));
			}

			if (path.Count != 0)
			{
				movePath(path);
			}
			return path;
		}

		protected List<Cell> workingWithDwellings()
		{
			var returnPath = new List<Cell>();
			var path = new List<Cell>();
			var dwellings = getAvailableDwellings(Finder.Cells);

			if (dwellings == null || dwellings.Count ==0) return returnPath;
			foreach (var dwelling in dwellings)
			{

				switch (dwelling.CellType.SubCellType)
				{
					case SubCellType.DwellingCavalry:
					{
						path = useDwelling(dwelling, UnitType.Cavalry, Resource.Ebony);
						if (path.Count != 0)
						{
							movePath(path);
							var unitsCount = getAmountOfUnitsToBuy(SubCellType.DwellingCavalry, dwelling);
							if (unitsCount > 0)
								SensorData = Client.HireUnits(unitsCount);
						}
						break;
					}

					case SubCellType.DwellingInfantry:
					{
						path = useDwelling(dwelling, UnitType.Infantry, Resource.Iron);
						if (path.Count != 0)
						{
							movePath(path);
							var unitsCount = getAmountOfUnitsToBuy(SubCellType.DwellingInfantry, dwelling);
							if (unitsCount > 0)
								SensorData = Client.HireUnits(unitsCount);
						}
						break;
					}

					case SubCellType.DwellingRanged:
					{
						path = useDwelling(dwelling, UnitType.Ranged, Resource.Glass);
						if (path.Count != 0)
						{
							movePath(path);
							var unitsCount = getAmountOfUnitsToBuy(SubCellType.DwellingRanged, dwelling);
							if (unitsCount > 0)
								SensorData = Client.HireUnits(unitsCount);
						}
						break;
					}
					case SubCellType.DwellingMilitia:
					{
						path = useDwelling(dwelling, UnitType.Militia, Resource.Gold);
						if (path.Count != 0)
						{
							movePath(path);
							var unitsCount = getAmountOfUnitsToBuy(SubCellType.DwellingMilitia, dwelling);
							if (unitsCount > 0)
								SensorData = Client.HireUnits(unitsCount);
						}
						break;
					}
				}
				returnPath.AddRange(path);
				UpdateMap();
			}
			return returnPath;
		}

		protected List<Cell> getAvailableDwellings(List<Cell> finderCells)
		{
			var availableDwellings = finderCells.Where(i => (i.CellType.MainType == MainCellType.Dwelling)
						   && !i.Value.Equals(Single.MaxValue) && (i.ResourcesValue > 0)).OrderBy(i => i.Value).ToList();

			if (availableDwellings.Count != 0)
			{
				var notMilitiaDwellings =
					availableDwellings.Where(i => (i.CellType.SubCellType != SubCellType.DwellingMilitia)).ToList();

				return notMilitiaDwellings.Count >0 ? notMilitiaDwellings : availableDwellings;
			}
			return null;
		}

		protected List<Cell> searchAvailableMines(List<Cell> finderCells)
		{
            var result = finderCells.Where(i => (i.CellType.MainType == MainCellType.Mine)
                          && i.CellType.SubCellType == SubCellType.MineGold
                          && !i.Value.Equals(Single.MaxValue)).ToList();
            if (result.Count == 0)
                result = finderCells.Where(i => (i.CellType.MainType == MainCellType.Mine)
                            && !i.Value.Equals(Single.MaxValue)).ToList();
            return result;
    }

		protected List<Cell> useDwelling(Cell dwellingCheck, UnitType unitType, Resource resource)
		{
			var path = new List<Cell>();
			var missingTreasury = getMissingTreasuryForDwelling(dwellingCheck, unitType, resource);
			path = missingTreasury.Count == 0 ? Finder.GetMovesStraightToCell(dwellingCheck) :
				findResourcesForDwelling(missingTreasury, dwellingCheck, resource);
			return path;
		}

		protected Dictionary<Resource, int> getMissingTreasuryForDwelling(Cell dwellingCheck, UnitType unitType, Resource resource = new Resource())
		{

			var missingResources = new Dictionary<Resource, int>();

			if (dwellingCheck.CellType.SubCellType == SubCellType.DwellingMilitia)
			{
				if (SensorData.MyTreasury[Resource.Gold] >= UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold])
				{
					return new Dictionary<Resource, int>();
				}
				missingResources.Add(Resource.Gold,
					UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold] - SensorData.MyTreasury[Resource.Gold]);
			}
			else
			{
				if (SensorData.MyTreasury[Resource.Gold] >= UnitsConstants.Current.UnitCost[unitType][Resource.Gold] &&
					SensorData.MyTreasury[resource] >= UnitsConstants.Current.UnitCost[unitType][resource])
				{
					return new Dictionary<Resource, int>();
				}

				missingResources.Add(Resource.Gold,
					UnitsConstants.Current.UnitCost[unitType][Resource.Gold] - SensorData.MyTreasury[Resource.Gold]);

				missingResources.Add(resource,
					UnitsConstants.Current.UnitCost[unitType][resource] - SensorData.MyTreasury[resource]);
			}

			return missingResources;
		}

		protected List<Cell> findResourcesForDwelling(Dictionary<Resource, int> missingTreasury,
			Cell dwelling, Resource resource)
		{
			var subCellType = new SubCellType();
			switch (resource)
			{
				case Resource.Ebony:
					subCellType = SubCellType.ResourceEbony;
					break;
				case Resource.Iron:
					subCellType = SubCellType.ResourceIron;
					break;
				case Resource.Glass:
					subCellType = SubCellType.ResourceGlass;
					break;
			}

			var resultCellsList = new List<Cell>();
			var foundedCells = Finder.Cells.Where(o => (o.CellType.SubCellType == SubCellType.ResourceGold && o.ResourcesValue > 0)
						&& !o.Value.Equals(Single.MaxValue) && !resultCellsList.Contains(o)).OrderBy(o => o.Value).ToList();

			for (int i = 0; i < foundedCells.Count && missingTreasury[Resource.Gold] > 0; i++)
			{
				var cell = foundedCells.ElementAt(i);
				resultCellsList.Add(cell);
				missingTreasury[Resource.Gold] = missingTreasury[Resource.Gold] - cell.ResourcesValue;
			}

			if (resource != Resource.Gold)
			{
				var resourceFinder = Finder;
				if (resultCellsList.Count > 0)
				{
					resourceFinder = new Finder(Finder.Cells, resultCellsList[0]);
				}
				foundedCells = resourceFinder.Cells.Where(o => (o.CellType.SubCellType == subCellType && o.ResourcesValue > 0)
						&& !o.Value.Equals(Single.MaxValue) && !resultCellsList.Contains(o)).OrderBy(o => o.Value).ToList();

				for (int i = 0; i < foundedCells.Count && missingTreasury[resource] > 0; i++)
				{
					var cell = foundedCells.ElementAt(i);
					resultCellsList.Add(cell);
					missingTreasury[resource] = missingTreasury[resource] - cell.ResourcesValue;
				}
			}
			var cellPath = new List<Cell>();
			if (checkCanDefeatAllPathEnemies(resultCellsList)) return cellPath;
			if (resultCellsList.Count <= 0) return cellPath;

			cellPath.AddRange(Finder.GetSmartPath(SensorData.Location.CreateCell(), resultCellsList[0]));

			for (int y = 1; y < resultCellsList.Count; y++)
			{
				if (!cellPath.Contains(resultCellsList[y]))
				{
					var finderNew = new Finder(Finder.Cells, resultCellsList[y]);
					cellPath.AddRange(finderNew.GetSmartPath(resultCellsList[y - 1], resultCellsList[y]));
				}
			}
			var finderToEnd = new Finder(Finder.Cells, resultCellsList[resultCellsList.Count - 1]);
			cellPath.AddRange(finderToEnd.GetSmartPath(resultCellsList[resultCellsList.Count - 1], dwelling));
			return cellPath;
		}

		protected bool checkCanDefeatAllPathEnemies(List<Cell> resultCellsList)
		{
			var enemyArmyCells = resultCellsList.FindAll(i => i.EnemyArmy?.Count != 0).ToList();
			var enemyStrength = new Dictionary<UnitType, int>();
			foreach (var enemyArmyCell in enemyArmyCells)
			{
				if(enemyArmyCell.EnemyArmy !=null)
				enemyStrength.Concat(enemyArmyCell.EnemyArmy);
			}
			return Combat.Resolve(new ArmiesPair(SensorData.MyArmy, enemyStrength)).IsDefenderWin;
		}

		protected int getAmountOfUnitsToBuy(SubCellType subCellType, Cell dwellingCheck)
		{
			if (subCellType == SubCellType.DwellingMilitia)
			{
				var amountOfUnitsToBuy = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Militia][Resource.Gold];
				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;
			}

			if (subCellType == SubCellType.DwellingCavalry)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Cavalry][Resource.Gold];
				var maxAmountEbony = SensorData.MyTreasury[Resource.Ebony] / UnitsConstants.Current.UnitCost[UnitType.Cavalry][Resource.Ebony];
				var amountOfUnitsToBuy = maxAmountGold < maxAmountEbony ? maxAmountGold : maxAmountEbony;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;
			}

			if (subCellType == SubCellType.DwellingInfantry)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Infantry][Resource.Gold];
				var maxAmountIron = SensorData.MyTreasury[Resource.Iron] / UnitsConstants.Current.UnitCost[UnitType.Infantry][Resource.Iron];
				var amountOfUnitsToBuy = maxAmountGold < maxAmountIron ? maxAmountGold : maxAmountIron;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;

			}

			if (subCellType == SubCellType.DwellingRanged)
			{
				var maxAmountGold = SensorData.MyTreasury[Resource.Gold] / UnitsConstants.Current.UnitCost[UnitType.Ranged][Resource.Gold];
				var maxAmountGlass = SensorData.MyTreasury[Resource.Glass] / UnitsConstants.Current.UnitCost[UnitType.Ranged][Resource.Glass];
				var amountOfUnitsToBuy = maxAmountGold < maxAmountGlass ? maxAmountGold : maxAmountGlass;

				return dwellingCheck.ResourcesValue >= amountOfUnitsToBuy ? amountOfUnitsToBuy : dwellingCheck.ResourcesValue;

			}

			return 0;
		}
		protected void movePath(List<Cell> path)
		{
			if (path.Count == 0) return;
			var steps = Converter.ConvertCellPathToDirection(path);
			for (var index = 0; index < steps.Count; index++)
			{
				var containsArmy = path[index + 1].EnemyArmy != null;
				SensorData = Client.Move(steps[index]);
				if (containsArmy)
				{
					UpdateMap();
					return;
				}
			}
			UpdateMap();
		}

		protected void moveOneStep(Direction direction)
		{
			SensorData = Client.Move(direction);
		}
		public abstract void IncreaseGamingPoints();
		public abstract void MakeDecisions();
		public abstract void Act(List<Cell> path);
	}
}
