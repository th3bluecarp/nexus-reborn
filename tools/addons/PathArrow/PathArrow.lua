-----------------------------------------------------------------------------------------------
-- PathArrow
-- A small movable arrow that points at the nearest active path mission location,
-- or at the nearest tracked quest objective. For the WildStar 16042 client.
--
-- Slash commands:
--   /patharrow          toggle the window
--   /patharrow next     point at the next mission (or quest) in distance order
--   /patharrow prev     point at the previous one
--   /patharrow quest    switch to tracked quest objectives
--   /patharrow path     switch back to path missions
--   /patharrow hint     also fire the game's own hint arrow for the current target
--   /patharrow flip     mirror the arrow left/right (only needed if it points mirrored)
--   /patharrow rad      toggle pixie rotation units degrees/radians (only if it spins wrong)
--   /patharrow reset    move the window back to the default spot
--
-- API sources (see README.md for the full confirmed/guessed list):
--   Hammster/wildstar-api-docs, the Carbine default UI addons (ZoneMap, PathTracker,
--   Stuck, AbilityVendor, Crafting) and RaidCore (DrawManager).
-----------------------------------------------------------------------------------------------

require "Window"
require "GameLib"
require "PlayerPathLib"
require "PathMission"
require "PathEpisode"
require "QuestLib"
require "Quest"

local PathArrow = {}

local knSaveVersion      = 1
local kfTickSeconds      = 0.1   -- arrow refresh
local knRescanTicks      = 20    -- rebuild the target list every 2 seconds
local kstrArrowSprite    = "CRB_Basekit:kitIcon_Holo_UpArrow" -- confirmed sprite name (Carbine Crafting.xml), art points up
local kfProbeMeters      = 2.0   -- length of the world step projected to screen to read the direction

local ktClock = { "12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" }

-----------------------------------------------------------------------------------------------
-- Small safe helpers. Every engine call goes through pcall so a nil or a missing
-- function shows "no location" instead of throwing a Lua error.
-----------------------------------------------------------------------------------------------
local function SafeCall(fn, ...)
	if type(fn) ~= "function" then return nil end
	local bOk, a, b = pcall(fn, ...)
	if bOk then return a, b end
	return nil
end

local function SafeMethod(obj, strMethod, ...)
	if obj == nil then return nil end
	local bOk, fn = pcall(function() return obj[strMethod] end)
	if not bOk or type(fn) ~= "function" then return nil end
	return SafeCall(fn, obj, ...)
end

-- Accepts a Vector3 or a plain {x,y,z} table. Returns a plain table or nil.
local function ToPoint(v)
	if v == nil then return nil end
	local bOk, x, y, z = pcall(function() return v.x, v.y, v.z end)
	if not bOk or type(x) ~= "number" or type(z) ~= "number" then return nil end
	return { x = x, y = (type(y) == "number") and y or 0, z = z }
end

local function FlatDistance(a, b)
	local dx, dz = b.x - a.x, b.z - a.z
	return math.sqrt(dx * dx + dz * dz)
end

local function Print(str)
	if ChatSystemLib and ChatSystemLib.PostOnChannel and ChatSystemLib.ChatChannel_System then
		pcall(ChatSystemLib.PostOnChannel, ChatSystemLib.ChatChannel_System, "[PathArrow] " .. str, "")
	end
end

-- Pull world points out of whatever a GetMapRegions / GetMapLocations call gave back.
-- Confirmed shapes (Carbine ZoneMap.lua):
--   PathMission:GetMapRegions() -> list of { tHexes, tIndicator }
--   Quest:GetMapRegions()       -> list of { nWorldId, nWorldZoneId, tRegions, tIndicator }
-- GUESSED shape: PathMission:GetMapLocations() is never called in the Carbine sources.
-- It is assumed to be either a list of points (like PublicEvent:GetLocations()) or
-- a list of tables carrying a point, so every likely field is tried.
local function CollectPoints(tList, tOut, nWorldId)
	if type(tList) ~= "table" then return end
	for _, entry in pairs(tList) do
		if type(entry) == "table" or type(entry) == "userdata" then
			local bWorldOk = true
			local nEntryWorld = nil
			pcall(function() nEntryWorld = entry.nWorldId end)
			if nWorldId and type(nEntryWorld) == "number" and nEntryWorld ~= nWorldId then
				bWorldOk = false
			end
			if bWorldOk then
				local pt = ToPoint(entry)
				if not pt then
					local tCandidates = {}
					pcall(function()
						tCandidates = { entry.tIndicator, entry.tLoc, entry.worldLoc, entry.tLocation, entry.tPosition }
					end)
					for i = 1, 5 do
						pt = ToPoint(tCandidates[i])
						if pt then break end
					end
				end
				if pt then table.insert(tOut, pt) end
			end
		end
	end
end

-----------------------------------------------------------------------------------------------
-- Init
-----------------------------------------------------------------------------------------------
function PathArrow:new(o)
	o = o or {}
	setmetatable(o, self)
	self.__index = self
	o.strMode = "path"
	o.nIndex = 1
	o.tTargets = {}
	o.nTicks = 0
	o.bFlip = false
	o.bRadians = false
	o.bShown = true
	return o
end

function PathArrow:Init()
	Apollo.RegisterAddon(self, false, "", {})
end

function PathArrow:OnLoad()
	self.xmlDoc = XmlDoc.CreateFromFile("PathArrow.xml")
	self.xmlDoc:RegisterCallback("OnDocumentReady", self)
end

function PathArrow:OnDocumentReady()
	if self.xmlDoc == nil or not self.xmlDoc:IsLoaded() then
		return
	end
	Apollo.RegisterSlashCommand("patharrow", "OnSlashCommand", self)
	self:CreateWindow()
	self.timerTick = ApolloTimer.Create(kfTickSeconds, true, "OnTick", self)
end

function PathArrow:CreateWindow()
	if self.wndMain and self.wndMain:IsValid() then return end
	self.wndMain = Apollo.LoadForm(self.xmlDoc, "PathArrowForm", nil, self)
	if not self.wndMain then return end
	if self.locSaved then
		self.wndMain:MoveToLocation(self.locSaved)
	end
	self.wndArrow = self.wndMain:FindChild("ArrowCanvas")
	self.wndTitle = self.wndMain:FindChild("TitleText")
	self.wndInfo = self.wndMain:FindChild("InfoText")
	self.wndModeText = self.wndMain:FindChild("ModeText")
	self.nPixieId = nil
	self.wndMain:Show(self.bShown, true)
end

-----------------------------------------------------------------------------------------------
-- Save / restore (window position and settings, per character)
-----------------------------------------------------------------------------------------------
function PathArrow:OnSave(eType)
	if eType ~= GameLib.CodeEnumAddonSaveLevel.Character then
		return
	end
	local loc = (self.wndMain and self.wndMain:IsValid()) and self.wndMain:GetLocation() or self.locSaved
	return {
		nVersion = knSaveVersion,
		tLocation = loc and loc:ToTable() or nil,
		strMode = self.strMode,
		bFlip = self.bFlip,
		bRadians = self.bRadians,
		bShown = self.bShown,
	}
end

function PathArrow:OnRestore(eType, tData)
	if eType ~= GameLib.CodeEnumAddonSaveLevel.Character or type(tData) ~= "table" then
		return
	end
	if tData.nVersion ~= knSaveVersion then return end
	if tData.tLocation then
		self.locSaved = WindowLocation.new(tData.tLocation)
		if self.wndMain and self.wndMain:IsValid() then
			self.wndMain:MoveToLocation(self.locSaved)
		end
	end
	if tData.strMode == "quest" or tData.strMode == "path" then self.strMode = tData.strMode end
	self.bFlip = tData.bFlip and true or false
	self.bRadians = tData.bRadians and true or false
	if tData.bShown ~= nil then self.bShown = tData.bShown and true or false end
	if self.wndMain and self.wndMain:IsValid() then
		self.wndMain:Show(self.bShown, true)
	end
end

-----------------------------------------------------------------------------------------------
-- Slash command
-----------------------------------------------------------------------------------------------
function PathArrow:OnSlashCommand(strCmd, strArg)
	local strSub = string.lower(strArg or "")
	strSub = string.gsub(strSub, "^%s+", "")
	strSub = string.gsub(strSub, "%s+$", "")
	self:CreateWindow()
	if not self.wndMain then return end

	if strSub == "" or strSub == "toggle" then
		self.bShown = not self.wndMain:IsShown()
		self.wndMain:Show(self.bShown, true)
		return
	end

	self.bShown = true
	self.wndMain:Show(true, true)

	if strSub == "next" then
		self:Rescan()
		self.nIndex = self.nIndex + 1
		if self.nIndex > #self.tTargets then self.nIndex = 1 end
		self.idLocked = self.tTargets[self.nIndex] and self.tTargets[self.nIndex].id or nil
	elseif strSub == "prev" then
		self:Rescan()
		self.nIndex = self.nIndex - 1
		if self.nIndex < 1 then self.nIndex = math.max(#self.tTargets, 1) end
		self.idLocked = self.tTargets[self.nIndex] and self.tTargets[self.nIndex].id or nil
	elseif strSub == "quest" or strSub == "quests" then
		self.strMode = "quest"
		self.nIndex = 1
		self.idLocked = nil
		Print("pointing at tracked quest objectives")
	elseif strSub == "path" or strSub == "mission" or strSub == "missions" then
		self.strMode = "path"
		self.nIndex = 1
		self.idLocked = nil
		Print("pointing at path missions")
	elseif strSub == "nearest" then
		self.nIndex = 1
		self.idLocked = nil
	elseif strSub == "hint" then
		local t = self.tTargets[self.nIndex]
		if t and t.obj then
			-- Confirmed: PathMission:ShowHintArrow() and Quest:ShowHintArrow() (PathTracker.lua, ContractTracker.lua)
			SafeMethod(t.obj, "ShowHintArrow")
		else
			Print("no target")
		end
	elseif strSub == "flip" then
		self.bFlip = not self.bFlip
		Print("arrow mirror " .. (self.bFlip and "on" or "off"))
	elseif strSub == "rad" then
		self.bRadians = not self.bRadians
		Print("pixie rotation units " .. (self.bRadians and "radians" or "degrees"))
	elseif strSub == "reset" then
		self.locSaved = nil
		self.wndMain:Destroy()
		self.wndMain = nil
		self:CreateWindow()
	else
		Print("commands: /patharrow [next | prev | nearest | quest | path | hint | flip | rad | reset]")
	end
	self:Rescan()
	self:Refresh()
end

-----------------------------------------------------------------------------------------------
-- Target lists
-----------------------------------------------------------------------------------------------
function PathArrow:GetPlayerPos()
	local unitPlayer = SafeCall(GameLib.GetPlayerUnit)
	if not unitPlayer then return nil, nil end
	return ToPoint(SafeMethod(unitPlayer, "GetPosition")), unitPlayer
end

function PathArrow:BuildMissionTargets(ptPlayer)
	local tOut = {}
	local epi = SafeCall(PlayerPathLib.GetCurrentEpisode)
	if not epi then return tOut, "no path episode in this zone" end
	local tMissions = SafeMethod(epi, "GetMissions")
	if type(tMissions) ~= "table" then return tOut, "no missions" end

	local eUnlocked = PathMission.PathMissionState_Unlocked
	local eStarted = PathMission.PathMissionState_Started

	for _, pm in ipairs(tMissions) do
		local eState = SafeMethod(pm, "GetMissionState")
		local bComplete = SafeMethod(pm, "IsComplete")
		-- Active = unlocked or started, and not complete (same test as Carbine ZoneMap:UpdateMissionList)
		if not bComplete and (eState == eUnlocked or eState == eStarted) then
			local tPoints = {}
			CollectPoints(SafeMethod(pm, "GetMapRegions"), tPoints)
			CollectPoints(SafeMethod(pm, "GetMapLocations"), tPoints)
			table.insert(tOut, self:MakeTarget(pm, SafeMethod(pm, "GetName"), tPoints, ptPlayer, SafeMethod(pm, "GetDistance")))
		end
	end
	return tOut, "no active path missions here"
end

function PathArrow:BuildQuestTargets(ptPlayer)
	local tOut = {}
	local nWorldId = SafeCall(GameLib.GetCurrentWorldId)
	local tEpisodes = SafeCall(QuestLib.GetTrackedEpisodes, false)
	if type(tEpisodes) ~= "table" then return tOut, "no tracked quests" end
	for _, epi in pairs(tEpisodes) do
		local tQuests = SafeMethod(epi, "GetTrackedQuests", 0, false)
		if type(tQuests) == "table" then
			for _, que in pairs(tQuests) do
				local eState = SafeMethod(que, "GetState")
				if eState ~= Quest.QuestState_Completed and eState ~= Quest.QuestState_Botched
					and eState ~= Quest.QuestState_Abandoned then
					local tPoints = {}
					CollectPoints(SafeMethod(que, "GetMapRegions"), tPoints, nWorldId)
					table.insert(tOut, self:MakeTarget(que, SafeMethod(que, "GetTitle"), tPoints, ptPlayer, SafeMethod(que, "GetDistance")))
				end
			end
		end
	end
	return tOut, "no tracked quests"
end

function PathArrow:MakeTarget(obj, strName, tPoints, ptPlayer, nEngineDist)
	-- GetId() is confirmed on both PathMission and Quest. The name is the fallback key.
	local id = SafeMethod(obj, "GetId")
	if id == nil then id = strName end
	local t = { obj = obj, id = id, strName = (type(strName) == "string" and strName ~= "") and strName or "(unnamed)", tPoints = tPoints }
	if ptPlayer then
		for _, pt in ipairs(tPoints) do
			local d = FlatDistance(ptPlayer, pt)
			if t.nDist == nil or d < t.nDist then
				t.nDist = d
				t.pt = pt
			end
		end
	end
	if t.nDist == nil and type(nEngineDist) == "number" then
		t.nEngineDist = nEngineDist
	end
	return t
end

function PathArrow:Rescan()
	local ptPlayer = self:GetPlayerPos()
	local tTargets, strEmpty
	if self.strMode == "quest" then
		tTargets, strEmpty = self:BuildQuestTargets(ptPlayer)
	else
		tTargets, strEmpty = self:BuildMissionTargets(ptPlayer)
	end
	table.sort(tTargets, function(a, b)
		-- Targets with a known location come first, then by distance.
		if (a.pt ~= nil) ~= (b.pt ~= nil) then return a.pt ~= nil end
		local da = a.nDist or a.nEngineDist or math.huge
		local db = b.nDist or b.nEngineDist or math.huge
		return da < db
	end)
	self.tTargets = tTargets
	self.strEmpty = strEmpty

	-- Keep pointing at the same mission after a resort if the player picked one with next/prev.
	self.nIndex = 1
	if self.idLocked then
		for i, t in ipairs(tTargets) do
			if t.id == self.idLocked then
				self.nIndex = i
				return
			end
		end
		self.idLocked = nil -- it finished or went away, fall back to nearest
	end
end

-----------------------------------------------------------------------------------------------
-- Direction
-----------------------------------------------------------------------------------------------
-- Returns the clockwise screen angle in degrees (0 = straight up = straight ahead) from the
-- player toward ptTarget, or nil.
function PathArrow:GetScreenAngle(ptPlayer, unitPlayer, ptTarget)
	local dx, dz = ptTarget.x - ptPlayer.x, ptTarget.z - ptPlayer.z
	local len = math.sqrt(dx * dx + dz * dz)
	if len < 0.01 then return 0 end
	dx, dz = dx / len, dz / len

	-- Method 1 (camera relative): project the player and a point 2 m toward the target onto
	-- the screen, then take the screen direction. Same math RaidCore uses for its oriented
	-- dot sprites (atan2 of the screen vector, plus 90). Confirmed functions:
	-- GameLib.WorldLocToScreenPoint(Vector3) and Vector3.New(x, y, z).
	if Vector3 and Vector3.New and GameLib.WorldLocToScreenPoint then
		local vA = SafeCall(Vector3.New, ptPlayer.x, ptPlayer.y, ptPlayer.z)
		local vB = SafeCall(Vector3.New, ptPlayer.x + dx * kfProbeMeters, ptPlayer.y, ptPlayer.z + dz * kfProbeMeters)
		local sA = ToPoint(SafeCall(GameLib.WorldLocToScreenPoint, vA))
		local sB = ToPoint(SafeCall(GameLib.WorldLocToScreenPoint, vB))
		if sA and sB and sA.z > 0 and sB.z > 0 then
			local vx, vy = sB.x - sA.x, sB.y - sA.y
			if math.abs(vx) + math.abs(vy) > 0.5 then
				return math.deg(math.atan2(vy, vx)) + 90
			end
		end
	end

	-- Method 2 (character relative, fallback when the probe is behind the camera):
	-- Unit:GetFacing() is confirmed (RaidCore uses it as a unit vector on the x/z plane).
	-- GUESSED: the sign of the turn. The world handedness is not documented, so the
	-- result can be mirrored. "/patharrow flip" fixes that in game.
	local f = ToPoint(SafeMethod(unitPlayer, "GetFacing"))
	if f then
		local cross = f.x * dz - f.z * dx
		local dot = f.x * dx + f.z * dz
		local a = -math.deg(math.atan2(cross, dot))
		if self.bFlip then a = -a end
		return a
	end
	return nil
end

local function ClockText(fAngle)
	local a = fAngle % 360
	local n = math.floor((a + 15) / 30) % 12
	return ktClock[n + 1] .. " o'clock"
end

-----------------------------------------------------------------------------------------------
-- Drawing
-----------------------------------------------------------------------------------------------
function PathArrow:SetArrow(fAngle)
	if not self.wndArrow or not self.wndArrow:IsValid() then return end
	if fAngle == nil then
		if self.nPixieId then
			pcall(self.wndArrow.DestroyPixie, self.wndArrow, self.nPixieId)
			self.nPixieId = nil
		end
		return
	end
	-- AddPixie / UpdatePixie with fRotation: confirmed in RaidCore and Carbine Crafting.
	-- GUESSED: units. The API docs and RaidCore say degrees, Carbine Crafting and AMPs
	-- code passes radians. Default is degrees, "/patharrow rad" switches.
	local fRot = fAngle
	if self.bRadians then fRot = math.rad(fAngle) end
	local tPixie = {
		strSprite = kstrArrowSprite,
		cr = "ffffffff",
		fRotation = fRot,
		loc = { fPoints = { 0, 0, 1, 1 }, nOffsets = { 0, 0, 0, 0 } },
	}
	if self.nPixieId then
		local bOk = pcall(self.wndArrow.UpdatePixie, self.wndArrow, self.nPixieId, tPixie)
		if bOk then return end
		self.nPixieId = nil
	end
	local bOk, id = pcall(self.wndArrow.AddPixie, self.wndArrow, tPixie)
	if bOk then self.nPixieId = id end
end

function PathArrow:OnTick()
	if not self.wndMain or not self.wndMain:IsValid() or not self.wndMain:IsShown() then
		return
	end
	self.nTicks = self.nTicks + 1
	if self.nTicks >= knRescanTicks or #self.tTargets == 0 then
		self.nTicks = 0
		self:Rescan()
	end
	self:Refresh()
end

function PathArrow:Refresh()
	if not self.wndMain or not self.wndMain:IsValid() then return end
	local strModeLabel = (self.strMode == "quest") and "Quest" or "Path"

	local t = self.tTargets[self.nIndex]
	if not t then
		self.wndTitle:SetText("PathArrow")
		self.wndInfo:SetText(self.strEmpty or "no location")
		self.wndModeText:SetText(strModeLabel .. "  0/0")
		self:SetArrow(nil)
		return
	end

	self.wndTitle:SetText(t.strName)
	self.wndModeText:SetText(string.format("%s  %d/%d", strModeLabel, self.nIndex, #self.tTargets))

	local ptPlayer, unitPlayer = self:GetPlayerPos()
	if not t.pt or not ptPlayer then
		if t.nEngineDist then
			self.wndInfo:SetText(string.format("no location  (%d m)", math.floor(t.nEngineDist + 0.5)))
		else
			self.wndInfo:SetText("no location")
		end
		self:SetArrow(nil)
		return
	end

	-- Re-pick the closest point of this target as the player moves.
	local nBest, ptBest = nil, nil
	for _, pt in ipairs(t.tPoints) do
		local d = FlatDistance(ptPlayer, pt)
		if nBest == nil or d < nBest then nBest, ptBest = d, pt end
	end
	t.pt, t.nDist = ptBest, nBest

	local fAngle = self:GetScreenAngle(ptPlayer, unitPlayer, ptBest)
	local strDist = string.format("%d m", math.floor(nBest + 0.5))
	local dy = ptBest.y - ptPlayer.y
	if dy > 10 then
		strDist = strDist .. string.format(", %d m up", math.floor(dy + 0.5))
	elseif dy < -10 then
		strDist = strDist .. string.format(", %d m down", math.floor(-dy + 0.5))
	end
	if nBest < 5 then
		strDist = "here  (" .. strDist .. ")"
	elseif fAngle then
		strDist = strDist .. "   " .. ClockText(fAngle)
	end
	self.wndInfo:SetText(strDist)
	self:SetArrow(fAngle)
end

local PathArrowInst = PathArrow:new()
PathArrowInst:Init()
