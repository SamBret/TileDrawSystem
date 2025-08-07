using Godot;
using Godot.Collections;
using System;
using System.Linq;

public partial class DrawTiles : Node2D
{
	/// <summary>
	/// The desired number of tiles along the width of the viewport
	/// </summary>
	[Export(PropertyHint.Range, "1, 100, suffix:tiles")]
	public float TilesPerWidth = 18;
	float TilesPerHeight;

	/// <summary>
	/// The size of each tile in the tilemaps being used
	/// </summary>
	[Export(PropertyHint.None, "suffix:px")]
	public int MapTileSize = 16;
	float tileSize;

	/// <summary>
	/// Filename can't be empty or contain any of the following characters: \/:*?"<>|
	/// </summary>
	[Export]
	public string FileName = "tileScene";

	/// <summary>
	/// Speed at which the grid moves when using WASD
	/// </summary>
	[Export(PropertyHint.Range, "0, 2000")]
	public int MoveSpeed = 1000;
	Vector2 position;

	/// <summary>
	/// Whether the grid can move vertically or not.
	/// </summary>
	[Export]
	public bool VerticalLocked = true;

	TileMapLayer visualTileLayer;
	TileMapLayer collisionTileLayer;

	Dictionary<string, Vector2I> tilesByName = new Dictionary<string, Vector2I>()
	{
		{ "NWConvex", new Vector2I(0, 0) },
		{ "Top", new Vector2I(1, 0) },
		{ "NEConvex", new Vector2I(2, 0) },
		{ "BackSlash", new Vector2I(3, 0) },
		{ "ForwardSlash", new Vector2I(4, 0) },
		{ "Left", new Vector2I(0, 1) },
		{ "Middle", new Vector2I(1, 1) },
		{ "Right", new Vector2I(2, 1) },
		{ "SEConcave", new Vector2I(3, 1) },
		{ "SWConcave", new Vector2I(4, 1) },
		{ "SWConvex", new Vector2I(0, 2) },
		{ "Bottom", new Vector2I(1, 2) },
		{ "SEConvex", new Vector2I(2, 2) },
		{ "NEConcave", new Vector2I(3, 2) },
		{ "NWConcave", new Vector2I(4, 2) },
	};

	#region Setup

	public override void _Ready()
	{
		tileSize = GetViewport().GetWindow().Size.X / TilesPerWidth;
		TilesPerHeight = GetViewport().GetWindow().Size.Y / tileSize + 1;

		GetTileLayers();

		PositionTileLayers();

		CalculateAll();
	}

	public void GetTileLayers()
	{
		visualTileLayer = GetChildren().OfType<TileMapLayer>().Where(tml => tml.Name == "VisualTileLayer").FirstOrDefault();
		collisionTileLayer = GetChildren().OfType<TileMapLayer>().Where(tml => tml.Name == "CollisionTileLayer").FirstOrDefault();
	}

	public void PositionTileLayers()
	{
		SetTileSizes();
		SetTileScales();
		SetTilePositions(true);
	}

	public void SetTileSizes()
	{
		visualTileLayer.TileSet.TileSize = new Vector2I(MapTileSize, MapTileSize);
		collisionTileLayer.TileSet.TileSize = new Vector2I(MapTileSize, MapTileSize);
	}
	public void SetTileScales()
	{
		visualTileLayer.Scale = new Vector2(tileSize, tileSize) / MapTileSize;
		collisionTileLayer.Scale = new Vector2(tileSize, tileSize) / MapTileSize;
	}

	public void SetTilePositions(bool firstTime = false)
	{
		if (firstTime)
		{
			visualTileLayer.Position = new Vector2(-tileSize / 2, -tileSize / 2);
			collisionTileLayer.Position = Vector2.Zero;
		}
		Position = position;
	}

	public void CalculateAll()
	{
		Array<Vector2I> usedCells = collisionTileLayer.GetUsedCells();
		int minX = usedCells.Min(cell => cell.X);
		int maxX = usedCells.Max(cell => cell.X);
		int minY = usedCells.Min(cell => cell.Y);
		int maxY = usedCells.Max(cell => cell.Y);
		for (int x = minX; x <= maxX + 1; x++)
		{
			for (int y = minY; y <= maxY + 1; y++)
			{
				CalculateVisualTile(new Vector2I(x, y));
			}
		}
	}

	#endregion

	#region Active Processes

	public override void _Process(double delta)
	{
		MoveGridInput();
		AddTileInput();
		RemoveTileInput();
	}

	public void MoveGridInput()
	{
		Vector2 moveVector = Vector2.Zero;
		if (Input.IsActionPressed("move_left"))
			moveVector.X -= 1;
		if (Input.IsActionPressed("move_right"))
			moveVector.X += 1;
		if (Input.IsActionPressed("move_up"))
			moveVector.Y -= 1;
		if (Input.IsActionPressed("move_down"))
			moveVector.Y += 1;
		moveVector = moveVector.Normalized();

		if (moveVector == Vector2.Zero)
			return;

		MovePosition(moveVector);

		SetTilePositions();
	}

	private void MovePosition(Vector2 moveVector)
	{
		position -= new Vector2(
			(float)(moveVector.X * MoveSpeed * GetProcessDeltaTime()),
			(float)(moveVector.Y * MoveSpeed * GetProcessDeltaTime())
		);

		if (VerticalLocked)
			position.Y = 0;
	}

	public void AddTileInput()
	{
		if (!Input.IsActionPressed("create"))
			return;
		if (GetViewport().GuiGetHoveredControl() != null)
			return;

		Vector2I tilePos = GetTilePosFromMouse();

		AddTile(tilePos);
	}

	public void RemoveTileInput()
	{
		if (!Input.IsActionPressed("delete"))
			return;
		if (GetViewport().GuiGetHoveredControl() != null)
			return;

		Vector2I tilePos = GetTilePosFromMouse();

		RemoveTile(tilePos);
	}

	public Vector2I GetTilePosFromMouse()
	{
		Vector2 mousePos = GetViewport().GetMousePosition() - position;
		Vector2I tilePos = new Vector2I(
			(int)Math.Floor(mousePos.X / (MapTileSize * visualTileLayer.Scale.X)),
			(int)Math.Floor(mousePos.Y / (MapTileSize * visualTileLayer.Scale.Y))
		);
		return tilePos;
	}

	public void AddTile(Vector2I tilePos)
	{
		collisionTileLayer.SetCell(tilePos, 0, new Vector2I(0, 0));
		CalculateVisualFromCollisionTile(tilePos);
	}

	public void RemoveTile(Vector2I tilePos)
	{
		collisionTileLayer.SetCell(tilePos, -1);
		CalculateVisualFromCollisionTile(tilePos);
	}

	public void CalculateVisualFromCollisionTile(Vector2I tilePos)
	{
		CalculateVisualTile(tilePos);
		CalculateVisualTile(new Vector2I(tilePos.X + 1, tilePos.Y));
		CalculateVisualTile(new Vector2I(tilePos.X, tilePos.Y + 1));
		CalculateVisualTile(new Vector2I(tilePos.X + 1, tilePos.Y + 1));
	}

	public void CalculateVisualTile(Vector2I tilePos)
	{
		Vector2I topLeftTilePos = new Vector2I(tilePos.X - 1, tilePos.Y - 1);
		Vector2I topRightTilePos = new Vector2I(tilePos.X, tilePos.Y - 1);
		Vector2I bottomLeftTilePos = new Vector2I(tilePos.X - 1, tilePos.Y);
		bool topLeftTile = collisionTileLayer.GetCellAtlasCoords(topLeftTilePos) == Vector2I.Zero;
		bool topRightTile = collisionTileLayer.GetCellAtlasCoords(topRightTilePos) == Vector2I.Zero;
		bool bottomLeftTile = collisionTileLayer.GetCellAtlasCoords(bottomLeftTilePos) == Vector2I.Zero;
		bool bottomRightTile = collisionTileLayer.GetCellAtlasCoords(tilePos) == Vector2I.Zero;

		int bitmask =
			(topLeftTile ? 1 : 0) << 3 |
			(topRightTile ? 1 : 0) << 2 |
			(bottomLeftTile ? 1 : 0) << 1 |
			(bottomRightTile ? 1 : 0) << 0;

		switch (bitmask)
		{
			case 0b0000:
				visualTileLayer.SetCell(tilePos, -1);
				break;
			case 0b0001:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["NWConvex"]);
				break;
			case 0b0010:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["NEConvex"]);
				break;
			case 0b0011:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["Top"]);
				break;
			case 0b0100:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["SWConvex"]);
				break;
			case 0b0101:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["Left"]);
				break;
			case 0b0110:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["ForwardSlash"]);
				break;
			case 0b0111:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["SEConcave"]);
				break;
			case 0b1000:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["SEConvex"]);
				break;
			case 0b1001:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["BackSlash"]);
				break;
			case 0b1010:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["Right"]);
				break;
			case 0b1011:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["SWConcave"]);
				break;
			case 0b1100:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["Bottom"]);
				break;
			case 0b1101:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["NEConcave"]);
				break;
			case 0b1110:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["NWConcave"]);
				break;
			case 0b1111:
				visualTileLayer.SetCell(tilePos, 0, tilesByName["Middle"]);
				break;
		}
	}

	#endregion

	#region Saving

	public void SaveClicked()
	{
		string result = SaveScene(FileName, true);
		if (result == "success")
		{
			GD.Print($"Scene saved as res://{FileName}.tscn");
		}
		else if (result == "empty")
		{
			GD.Print("Filename cannot be empty.");
		}
		else if (result == "invalid")
		{
			GD.Print("Filename contains invalid characters.");
		}
		else if (result == "exists")
		{
			GD.Print($"File res://{FileName}.tscn already exists. Please choose a different name or overwrite it.");
		}
		else
		{
			GD.Print("An unknown error occurred while saving the scene.");
		}
	}

	public void SaveAsClicked()
	{

	}

	private string SaveScene(string filename, bool overwrite = false)
	{
		filename = filename.Trim();
		if (string.IsNullOrWhiteSpace(filename))
			return "empty";
		if (FilenameContainsInvalidCharacters(filename))
			return "invalid";
		if (!overwrite && FileAccess.FileExists($"res://{filename}.tscn"))
			return "exists";

		Node tempRoot = new Node();
		TileMapLayer clonedVisual = (TileMapLayer)visualTileLayer.Duplicate();
		TileMapLayer clonedCollision = (TileMapLayer)collisionTileLayer.Duplicate();
		tempRoot.AddChild(clonedVisual);
		tempRoot.AddChild(clonedCollision);
		clonedVisual.Owner = tempRoot;
		clonedCollision.Owner = tempRoot;

		PackedScene scene = new PackedScene();
		scene.Pack(tempRoot);
		ResourceSaver.Save(scene, $"res://{filename}.tscn");

		tempRoot.QueueFree();

		return "success";
	}

	private bool FilenameContainsInvalidCharacters(string filename)
	{
		return filename.Contains('\\') || filename.Contains('/') || filename.Contains(':') || filename.Contains('*') || filename.Contains('?') || filename.Contains('\"') || filename.Contains('<') || filename.Contains('>') || filename.Contains('|');
	}

	#endregion
}
