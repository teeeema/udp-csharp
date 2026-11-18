-- Install as a Wireshark Lua plugin or run: wireshark -X lua_script:Tools/pr6_dissector.lua
local p = Proto("dustline6", "DUSTLINE secure UDP")
local f = {
 magic=ProtoField.string("dustline6.magic","Magic"), version=ProtoField.uint8("dustline6.version","Version"),
 type=ProtoField.uint8("dustline6.type","Type",base.DEC,{[10]="KEY_EXCHANGE",[11]="ENCRYPTED"}),
 kind=ProtoField.uint8("dustline6.kind","Kind"), direction=ProtoField.uint8("dustline6.direction","Direction"),
 session=ProtoField.uint64("dustline6.session","Session ID",base.HEX), size=ProtoField.uint16("dustline6.size","Payload size"),
 reserved=ProtoField.uint16("dustline6.reserved","Reserved"), nonce=ProtoField.bytes("dustline6.nonce","Nonce (12 bytes)"),
 ciphertext=ProtoField.bytes("dustline6.ciphertext","Ciphertext"), tag=ProtoField.bytes("dustline6.tag","GCM tag (16 bytes)"),
 handshake=ProtoField.bytes("dustline6.handshake","Public handshake payload")
}
local fields={};for _,field in pairs(f) do fields[#fields+1]=field end;p.fields=fields
function p.dissector(buf,pinfo,tree)
 if buf:len()<20 or buf(0,4):string()~="DSL6" then return 0 end
 pinfo.cols.protocol="DUSTLINE6"
 local t=tree:add(p,buf());local h=t:add(buf(0,20),"Open header / GCM AAD (20 bytes)")
 h:add(f.magic,buf(0,4));h:add(f.version,buf(4,1));h:add(f.type,buf(5,1));h:add(f.kind,buf(6,1))
 h:add(f.direction,buf(7,1));h:add(f.session,buf(8,8));h:add(f.size,buf(16,2));h:add(f.reserved,buf(18,2))
 if buf(5,1):uint()==11 and buf:len()>=48 then
  t:add(f.nonce,buf(20,12));t:add(f.ciphertext,buf(32,buf:len()-48));t:add(f.tag,buf(buf:len()-16,16))
 else t:add(f.handshake,buf(20)) end
 return buf:len()
end
local udp=DissectorTable.get("udp.port")
for _,port in ipairs({7777,7906,7907}) do udp:add(port,p) end
-- USER0 relay exports have no IP or UDP header. Their timestamps are relative.
DissectorTable.get("wtap_encap"):add(wtap.USER0,p)
