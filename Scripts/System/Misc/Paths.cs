using CalcMoves = Server.Movement.Movement;
using MoveImpl = Server.Movement.MovementImpl;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Movement;
using Server.PathAlgorithms.FastAStar;
using Server.PathAlgorithms.SlowAStar;
using Server.PathAlgorithms;
using Server.Targeting;
using Server;
using System.Collections.Generic;
using System.Collections;
using System;

namespace Server.PathAlgorithms.FastAStar
{
	public struct PathNode
	{
		public int cost, total;
		public int parent, next, prev;
		public int z;
	}

	public class FastAStarAlgorithm : PathAlgorithm
	{
		public static PathAlgorithm Instance = new FastAStarAlgorithm();

		private const int MaxDepth = 300;
		private const int AreaSize = 38;

		private const int NodeCount = AreaSize * AreaSize * PlaneCount;

		private const int PlaneOffset = 128;
		private const int PlaneCount = 13;
		private const int PlaneHeight = 20;

		private static Direction[] m_Path = new Direction[AreaSize * AreaSize];
		private static PathNode[] m_Nodes = new PathNode[NodeCount];
		private static BitArray m_Touched = new BitArray( NodeCount );
		private static BitArray m_OnOpen = new BitArray( NodeCount );
		private static int[] m_Successors = new int[8];

		private static int m_xOffset, m_yOffset;
		private static int m_OpenList;

		private Point3D m_Goal;

		public int Heuristic( int x, int y, int z )
		{
			x -= m_Goal.X - m_xOffset;
			y -= m_Goal.Y - m_yOffset;
			z -= m_Goal.Z;

			x *= 11;
			y *= 11;

			return (x*x)+(y*y)+(z*z);
		}

		public override bool CheckCondition( Mobile m, Map map, Point3D start, Point3D goal )
		{
			return Utility.InRange( start, goal, AreaSize );
		}

		private void RemoveFromChain( int node )
		{
			if ( node < 0 || node >= NodeCount )
				return;

			if ( !m_Touched[node] || !m_OnOpen[node] )
				return;

			int prev = m_Nodes[node].prev;
			int next = m_Nodes[node].next;

			if ( m_OpenList == node )
				m_OpenList = next;

			if ( prev != -1 )
				m_Nodes[prev].next = next;

			if ( next != -1 )
				m_Nodes[next].prev = prev;

			m_Nodes[node].prev = -1;
			m_Nodes[node].next = -1;
		}

		private void AddToChain( int node )
		{
			if ( node < 0 || node >= NodeCount )
				return;

			RemoveFromChain( node );

			if ( m_OpenList != -1 )
				m_Nodes[m_OpenList].prev = node;

			m_Nodes[node].next = m_OpenList;
			m_Nodes[node].prev = -1;

			m_OpenList = node;

			m_Touched[node] = true;
			m_OnOpen[node] = true;
		}

		public override Direction[] Find( Mobile m, Map map, Point3D start, Point3D goal )
		{
			if ( !Utility.InRange( start, goal, AreaSize ) )
				return null;

			m_Touched.SetAll( false );

			m_Goal = goal;

			m_xOffset = (start.X + goal.X - AreaSize) / 2;
			m_yOffset = (start.Y + goal.Y - AreaSize) / 2;

			int fromNode = GetIndex( start.X, start.Y, start.Z );
			int destNode = GetIndex( goal.X, goal.Y, goal.Z );

			m_OpenList = fromNode;

			m_Nodes[m_OpenList].cost = 0;
			m_Nodes[m_OpenList].total = Heuristic( start.X - m_xOffset, start.Y - m_yOffset, start.Z );
			m_Nodes[m_OpenList].parent = -1;
			m_Nodes[m_OpenList].next = -1;
			m_Nodes[m_OpenList].prev = -1;
			m_Nodes[m_OpenList].z = start.Z;

			m_OnOpen[m_OpenList] = true;
			m_Touched[m_OpenList] = true;

			BaseCreature bc = m as BaseCreature;

			int pathCount, parent;
			int backtrack = 0, depth = 0;

			Direction[] path = m_Path;

			while ( m_OpenList != -1 )
			{
				int bestNode = FindBest( m_OpenList );

				if ( ++depth > MaxDepth )
					break;

				if ( bc != null )
				{
					MoveImpl.AlwaysIgnoreDoors = bc.CanOpenDoors;
					MoveImpl.IgnoreMovableImpassables = bc.CanMoveOverObstacles;
				}

				int[] vals = m_Successors;
				int count = GetSuccessors( bestNode, m, map );

				MoveImpl.AlwaysIgnoreDoors = false;
				MoveImpl.IgnoreMovableImpassables = false;

				if ( count == 0 )
					break;

				for ( int i = 0; i < count; ++i )
				{
					int newNode = vals[i];

					bool wasTouched = m_Touched[newNode];

					if ( !wasTouched )
					{
						int newCost = m_Nodes[bestNode].cost + 1;
						int newTotal = newCost + Heuristic( newNode % AreaSize, (newNode / AreaSize) % AreaSize, m_Nodes[newNode].z );

						if ( !wasTouched || m_Nodes[newNode].total > newTotal )
						{
							m_Nodes[newNode].parent = bestNode;
							m_Nodes[newNode].cost = newCost;
							m_Nodes[newNode].total = newTotal;

							if ( !wasTouched || !m_OnOpen[newNode] )
							{
								AddToChain( newNode );

								if ( newNode == destNode )
								{
									pathCount = 0;
									parent = m_Nodes[newNode].parent;

									while ( parent != -1 )
									{
										path[pathCount++] = GetDirection( parent % AreaSize, (parent / AreaSize) % AreaSize, newNode % AreaSize, (newNode / AreaSize) % AreaSize );
										newNode = parent;
										parent = m_Nodes[newNode].parent;

										if ( newNode == fromNode )
											break;
									}

									Direction[] dirs = new Direction[pathCount];

									while ( pathCount > 0 )
										dirs[backtrack++] = path[--pathCount];

									return dirs;
								}
							}
						}
					}
				}
			}

			return null;
		}

		private int GetIndex( int x, int y, int z )
		{
			x -= m_xOffset;
			y -= m_yOffset;
			z += PlaneOffset;
			z /= PlaneHeight;

			return x + (y * AreaSize) + (z * AreaSize * AreaSize);
		}

		private int FindBest( int node )
		{
			int least = m_Nodes[node].total;
			int leastNode = node;

			while ( node != -1 )
			{
				if ( m_Nodes[node].total < least )
				{
					least = m_Nodes[node].total;
					leastNode = node;
				}

				node = m_Nodes[node].next;
			}

			RemoveFromChain( leastNode );

			m_Touched[leastNode] = true;
			m_OnOpen[leastNode] = false;

			return leastNode;
		}

		public int GetSuccessors( int p, Mobile m, Map map )
		{
			int px = p % AreaSize;
			int py = (p / AreaSize) % AreaSize;
			int pz = m_Nodes[p].z;
			int x, y, z;

			Point3D p3D = new Point3D( px + m_xOffset, py + m_yOffset, pz );

			int[] vals = m_Successors;
			int count = 0;

			for ( int i = 0; i < 8; ++i )
			{
				switch ( i )
				{
					default:
					case 0: x =  0; y = -1; break;
					case 1: x =  1; y = -1; break;
					case 2: x =  1; y =  0; break;
					case 3: x =  1; y =  1; break;
					case 4: x =  0; y =  1; break;
					case 5: x = -1; y =  1; break;
					case 6: x = -1; y =  0; break;
					case 7: x = -1; y = -1; break;
				}

				x += px;
				y += py;

				if ( x < 0 || x >= AreaSize || y < 0 || y >= AreaSize )
					continue;

				if ( CalcMoves.CheckMovement( m, map, p3D, (Direction)i, out z ) )
				{
					int idx = GetIndex( x + m_xOffset, y + m_yOffset, z );

					if ( idx >= 0 && idx < NodeCount )
					{
						m_Nodes[idx].z = z;
						vals[count++] = idx;
					}
				}
			}

			return count;
		}
	}
}

namespace Server.PathAlgorithms
{
	public abstract class PathAlgorithm
	{
		public abstract bool CheckCondition( Mobile m, Map map, Point3D start, Point3D goal );
		public abstract Direction[] Find( Mobile m, Map map, Point3D start, Point3D goal );

		private static Direction[] m_CalcDirections = new Direction[9]
			{
				Direction.Up,
				Direction.North,
				Direction.Right,
				Direction.West,
				Direction.North,
				Direction.East,
				Direction.Left,
				Direction.South,
				Direction.Down
			};

		public Direction GetDirection( int xSource, int ySource, int xDest, int yDest )
		{
			int x = xDest + 1 - xSource;
			int y = yDest + 1 - ySource;
			int v = (y * 3) + x;

			if ( v < 0 || v >= 9 )
				return Direction.North;

			return m_CalcDirections[v];
		}
	}
}

namespace Server.PathAlgorithms.SlowAStar
{
	public struct PathNode
	{
		public int x, y, z;
		public int g, h;
		public int px, py, pz;
		public int dir;
	}

	public class SlowAStarAlgorithm : PathAlgorithm
	{
		public static PathAlgorithm Instance = new SlowAStarAlgorithm();

		private const int MaxDepth = 300;
		private const int MaxNodes = MaxDepth * 16;

		private static PathNode[] m_Closed = new PathNode[MaxNodes];
		private static PathNode[] m_Open = new PathNode[MaxNodes];
		private static PathNode[] m_Successors = new PathNode[8];
		private static Direction[] m_Path = new Direction[MaxNodes];

		private Point3D m_Goal;

		public int Heuristic( int x, int y, int z )
		{
			x -= m_Goal.X;
			y -= m_Goal.Y;
			z -= m_Goal.Z;

			x *= 11;
			y *= 11;

			return (x*x)+(y*y)+(z*z);
		}

		public override bool CheckCondition( Mobile m, Map map, Point3D start, Point3D goal )
		{
			return false;
		}

		public override Direction[] Find( Mobile m, Map map, Point3D start, Point3D goal )
		{
			m_Goal = goal;

			BaseCreature bc = m as BaseCreature;

			PathNode curNode;

			PathNode goalNode = new PathNode();
			goalNode.x = goal.X;
			goalNode.y = goal.Y;
			goalNode.z = goal.Z;

			PathNode startNode = new PathNode();
			startNode.x = start.X;
			startNode.y = start.Y;
			startNode.z = start.Z;
			startNode.h = Heuristic( startNode.x, startNode.y, startNode.z );

			PathNode[] closed = m_Closed, open = m_Open, successors = m_Successors;
			Direction[] path = m_Path;

			int closedCount = 0, openCount = 0, sucCount = 0, pathCount = 0;
			int popIndex, curF;
			int x, y, z;
			int depth = 0;

			int xBacktrack, yBacktrack, zBacktrack, iBacktrack = 0;

			open[openCount++] = startNode;

			while ( openCount > 0 )
			{
				curNode = open[0];
				curF = curNode.g + curNode.h;
				popIndex = 0;

				for ( int i = 1; i < openCount; ++i )
				{
					if ( (open[i].g + open[i].h) < curF )
					{
						curNode = open[i];
						curF = curNode.g + curNode.h;
						popIndex = i;
					}
				}

				if ( curNode.x == goalNode.x && curNode.y == goalNode.y && Math.Abs( curNode.z-goalNode.z ) < 16 )
				{
					if ( closedCount == MaxNodes )
						break;

					closed[closedCount++] = curNode;

					xBacktrack = curNode.px;
					yBacktrack = curNode.py;
					zBacktrack = curNode.pz;

					if ( pathCount == MaxNodes )
						break;

					path[pathCount++] = (Direction)curNode.dir;

					while ( xBacktrack != startNode.x || yBacktrack != startNode.y || zBacktrack != startNode.z )
					{
						bool found = false;

						for ( int j = 0; !found && j < closedCount; ++j )
						{
							if ( closed[j].x == xBacktrack && closed[j].y == yBacktrack && closed[j].z == zBacktrack )
							{
								if ( pathCount == MaxNodes )
									break;

								curNode = closed[j];
								path[pathCount++] = (Direction)curNode.dir;
								xBacktrack = curNode.px;
								yBacktrack = curNode.py;
								zBacktrack = curNode.pz;
								found = true;
							}
						}

						if ( !found )
						{
							Console.WriteLine( "bugaboo.." );
							return null;
						}

						if ( pathCount == MaxNodes )
							break;
					}

					if ( pathCount == MaxNodes )
						break;

					Direction[] dirs = new Direction[pathCount];

					while ( pathCount > 0 )
						dirs[iBacktrack++] = path[--pathCount];

					return dirs;
				}

				--openCount;

				for ( int i = popIndex; i < openCount; ++i )
					open[i] = open[i + 1];

				sucCount = 0;

				if ( bc != null )
				{
					MoveImpl.AlwaysIgnoreDoors = bc.CanOpenDoors;
					MoveImpl.IgnoreMovableImpassables = bc.CanMoveOverObstacles;
				}

				for ( int i = 0; i < 8; ++i )
				{
					switch ( i )
					{
						default:
						case 0: x =  0; y = -1; break;
						case 1: x =  1; y = -1; break;
						case 2: x =  1; y =  0; break;
						case 3: x =  1; y =  1; break;
						case 4: x =  0; y =  1; break;
						case 5: x = -1; y =  1; break;
						case 6: x = -1; y =  0; break;
						case 7: x = -1; y = -1; break;
					}

					if ( CalcMoves.CheckMovement( m, map, new Point3D( curNode.x, curNode.y, curNode.z ), (Direction)i, out z ) )
					{
						successors[sucCount].x = x + curNode.x;
						successors[sucCount].y = y + curNode.y;
						successors[sucCount++].z = z;
					}
				}

				MoveImpl.AlwaysIgnoreDoors = false;
				MoveImpl.IgnoreMovableImpassables = false;

				if ( sucCount == 0 || ++depth > MaxDepth )
					break;

				for ( int i = 0; i < sucCount; ++i )
				{
					x = successors[i].x;
					y = successors[i].y;
					z = successors[i].z;

					successors[i].g = curNode.g + 1;

					int openIndex = -1, closedIndex = -1;

					for ( int j = 0; openIndex == -1 && j < openCount; ++j )
					{
						if ( open[j].x == x && open[j].y == y && open[j].z == z )
							openIndex = j;
					}

					if ( openIndex >= 0 && open[openIndex].g < successors[i].g )
						continue;

					for ( int j = 0; closedIndex == -1 && j < closedCount; ++j )
					{
						if ( closed[j].x == x && closed[j].y == y && closed[j].z == z )
							closedIndex = j;
					}

					if ( closedIndex >= 0 && closed[closedIndex].g < successors[i].g )
						continue;

					if ( openIndex >= 0 )
					{
						--openCount;

						for ( int j = openIndex; j < openCount; ++j )
							open[j] = open[j + 1];
					}

					if ( closedIndex >= 0 )
					{
						--closedCount;

						for ( int j = closedIndex; j < closedCount; ++j )
							closed[j] = closed[j + 1];
					}

					successors[i].px = curNode.x;
					successors[i].py = curNode.y;
					successors[i].pz = curNode.z;
					successors[i].dir = (int)GetDirection( curNode.x, curNode.y, x, y );
					successors[i].h = Heuristic( x, y, z );

					if ( openCount == MaxNodes )
						break;

					open[openCount++] = successors[i];
				}

				if ( openCount == MaxNodes || closedCount == MaxNodes )
					break;

				closed[closedCount++] = curNode;
			}

			return null;
		}
	}
}

namespace Server
{
	public delegate MoveResult MoveMethod( Direction d );

	public enum MoveResult
	{
		BadState,
		Blocked,
		Success,
		SuccessAutoTurn
	}
}

namespace Server
{
	public sealed class MovementPath
	{
		private Map m_Map;
		private Point3D m_Start;
		private Point3D m_Goal;
		private Direction[] m_Directions;

		public Map Map{ get{ return m_Map; } }
		public Point3D Start{ get{ return m_Start; } }
		public Point3D Goal{ get{ return m_Goal; } }
		public Direction[] Directions{ get{ return m_Directions; } }
		public bool Success{ get{ return ( m_Directions != null && m_Directions.Length > 0 ); } }

		public static void Initialize()
		{
			CommandSystem.Register( "Path", AccessLevel.GameMaster, new CommandEventHandler( Path_OnCommand ) );
		}

		public static void Path_OnCommand( CommandEventArgs e )
		{
			e.Mobile.BeginTarget( -1, true, TargetFlags.None, new TargetCallback( Path_OnTarget ) );
			e.Mobile.SendMessage( "Target a location and a path will be drawn there." );
		}

		private static void Path( Mobile from, IPoint3D p, PathAlgorithm alg, string name, int zOffset )
		{
			m_OverrideAlgorithm = alg;

			long start = DateTime.Now.Ticks;
			MovementPath path = new MovementPath( from, new Point3D( p ) );
			long end = DateTime.Now.Ticks;
			double len = Math.Round( (end-start) / 10000.0, 2 );

			if ( !path.Success )
			{
				from.SendMessage( "{0} path failed: {1}ms", name, len );
			}
			else
			{
				from.SendMessage( "{0} path success: {1}ms", name, len );

				int x = from.X;
				int y = from.Y;
				int z = from.Z;

				for ( int i = 0; i < path.Directions.Length; ++i )
				{
					Movement.Movement.Offset( path.Directions[i], ref x, ref y );

					new Items.RecallRune().MoveToWorld( new Point3D( x, y, z+zOffset ), from.Map );
				}
			}
		}

		public static void Path_OnTarget( Mobile from, object obj )
		{
			IPoint3D p = obj as IPoint3D;

			if ( p == null )
				return;

			Spells.SpellHelper.GetSurfaceTop( ref p );

			Path( from, p, FastAStarAlgorithm.Instance, "Fast", 0 );
			Path( from, p, SlowAStarAlgorithm.Instance, "Slow", 2 );
			m_OverrideAlgorithm = null;

			/*MovementPath path = new MovementPath( from, new Point3D( p ) );

			if ( !path.Success )
			{
				from.SendMessage( "No path to there could be found." );
			}
			else
			{
				//for ( int i = 0; i < path.Directions.Length; ++i )
				//	Timer.DelayCall( TimeSpan.FromSeconds( 0.1 + (i * 0.3) ), new TimerStateCallback( Pathfind ), new object[]{ from, path.Directions[i] } );
				int x = from.X;
				int y = from.Y;
				int z = from.Z;

				for ( int i = 0; i < path.Directions.Length; ++i )
				{
					Movement.Movement.Offset( path.Directions[i], ref x, ref y );

					new Items.RecallRune().MoveToWorld( new Point3D( x, y, z ), from.Map );
				}
			}*/
		}

		public static void Pathfind( object state )
		{
			object[] states = (object[])state;
			Mobile from = (Mobile) states[0];
			Direction d = (Direction) states[1];

			try
			{
				from.Direction = d;
				from.NetState.BlockAllPackets=true;
				from.Move( d );
				from.NetState.BlockAllPackets=false;
				from.ProcessDelta();
			}
			catch
			{
			}
		}

		private static PathAlgorithm m_OverrideAlgorithm;

		public static PathAlgorithm OverrideAlgorithm
		{
			get{ return m_OverrideAlgorithm; }
			set{ m_OverrideAlgorithm = value; }
		}

		public MovementPath( Mobile m, Point3D goal )
		{
			Point3D start = m.Location;
			Map map = m.Map;

			m_Map = map;
			m_Start = start;
			m_Goal = goal;

			if ( map == null || map == Map.Internal )
				return;

			if ( Utility.InRange( start, goal, 1 ) )
				return;

			try
			{
				PathAlgorithm alg = m_OverrideAlgorithm;

				if ( alg == null )
				{
					alg = FastAStarAlgorithm.Instance;

					//if ( !alg.CheckCondition( m, map, start, goal ) )	// SlowAstar is still broken
					//	alg = SlowAStarAlgorithm.Instance;		// TODO: Fix SlowAstar
				}

				if ( alg != null && alg.CheckCondition( m, map, start, goal ) )
					m_Directions = alg.Find( m, map, start, goal );
			}
			catch ( Exception e )
			{
				Console.WriteLine( "Warning: {0}: Pathing error from {1} to {2}", e.GetType().Name, start, goal );
			}
		}
	}
}

namespace Server
{
	public class PathFollower
	{
		// Should we use pathfinding? 'false' for not
		private static bool Enabled = true;

		private Mobile m_From;
		private IPoint3D m_Goal;
		private MovementPath m_Path;
		private int m_Index;
		private Point3D m_Next, m_LastGoalLoc;
		private DateTime m_LastPathTime;
		private MoveMethod m_Mover;

		public MoveMethod Mover
		{
			get{ return m_Mover; }
			set{ m_Mover = value; }
		}

		public IPoint3D Goal
		{
			get{ return m_Goal; }
		}

		public PathFollower( Mobile from, IPoint3D goal )
		{
			m_From = from;
			m_Goal = goal;
		}

		public MoveResult Move( Direction d )
		{
			if ( m_Mover == null )
				return ( m_From.Move( d ) ? MoveResult.Success : MoveResult.Blocked );

			return m_Mover( d );
		}

		public Point3D GetGoalLocation()
		{
			if ( m_Goal is Item )
				return ((Item)m_Goal).GetWorldLocation();

			return new Point3D( m_Goal );
		}

		private static TimeSpan RepathDelay = TimeSpan.FromSeconds( 2.0 );

		public void Advance( ref Point3D p, int index )
		{
			if ( m_Path != null && m_Path.Success )
			{
				Direction[] dirs = m_Path.Directions;

				if ( index >= 0 && index < dirs.Length )
				{
					int x = p.X, y = p.Y;

					CalcMoves.Offset( dirs[index], ref x, ref y );

					p.X = x;
					p.Y = y;
				}
			}
		}

		public void ForceRepath()
		{
			m_Path = null;
		}

		public bool CheckPath()
		{
			if ( !Enabled )
				return false;

			bool repath = false;

			Point3D goal = GetGoalLocation();

			if ( m_Path == null )
				repath = true;
			else if ( (!m_Path.Success || goal != m_LastGoalLoc) && (m_LastPathTime + RepathDelay) <= DateTime.Now )
				repath = true;
			else if ( m_Path.Success && Check( m_From.Location, m_LastGoalLoc, 0 ) )
				repath = true;

			if ( !repath )
				return false;

			m_LastPathTime = DateTime.Now;
			m_LastGoalLoc = goal;

			m_Path = new MovementPath( m_From, goal );

			m_Index = 0;
			m_Next = m_From.Location;

			Advance( ref m_Next, m_Index );

			return true;
		}

		public bool Check( Point3D loc, Point3D goal, int range )
		{
			if ( !Utility.InRange( loc, goal, range ) )
				return false;

			if ( range <= 1 && Math.Abs( loc.Z - goal.Z ) >= 16 )
				return false;

			return true;
		}

		public bool Follow( bool run, int range )
		{
			Point3D goal = GetGoalLocation();
			Direction d;

			if ( Check( m_From.Location, goal, range ) )
				return true;

			bool repathed = CheckPath();

			if ( !Enabled || !m_Path.Success )
			{
				d = m_From.GetDirectionTo( goal );

				if ( run )
					d |= Direction.Running;

				m_From.SetDirection( d );
				Move( d );

				return Check( m_From.Location, goal, range );
			}

			d = m_From.GetDirectionTo( m_Next );

			if ( run )
				d |= Direction.Running;

			m_From.SetDirection( d );

			MoveResult res = Move( d );

			if ( res == MoveResult.Blocked )
			{
				if ( repathed )
					return false;

				m_Path = null;
				CheckPath();

				if ( !m_Path.Success )
				{
					d = m_From.GetDirectionTo( goal );

					if ( run )
						d |= Direction.Running;

					m_From.SetDirection( d );
					Move( d );

					return Check( m_From.Location, goal, range );
				}

				d = m_From.GetDirectionTo( m_Next );

				if ( run )
					d |= Direction.Running;

				m_From.SetDirection( d );

				res = Move( d );

				if ( res == MoveResult.Blocked )
					return false;
			}

			if ( m_From.X == m_Next.X && m_From.Y == m_Next.Y )
			{
				if ( m_From.Z == m_Next.Z )
				{
					++m_Index;
					Advance( ref m_Next, m_Index );
				}
				else
				{
					m_Path = null;
				}
			}

			return Check( m_From.Location, goal, range );
		}
	}
}
