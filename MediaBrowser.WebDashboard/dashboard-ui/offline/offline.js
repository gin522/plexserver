define(["globalize","connectionManager","focusManager","cardBuilder","emby-itemscontainer","flexStyles","scrollStyles"],function(globalize,connectionManager,focusManager,cardBuilder){"use strict";return function(view,params){function mergeInto(list1,list2){for(var i=0,length=list2.length;i<length;i++)list1.push(list2[i])}function getLatestItems(type){var promises=connectionManager.getApiClients().map(function(apiClient){return apiClient.getLatestOfflineItems({MediaType:type,Limit:20})});return Promise.all(promises).then(function(responses){for(var items=[],i=0,length=responses.length;i<length;i++)mergeInto(items,responses[i]);return items})}function loadLatestSection(section){return getLatestItems(section.getAttribute("data-mediatype")).then(function(items){return cardBuilder.buildCards(items,{parentContainer:section,itemsContainer:section.querySelector(".itemsContainer"),shape:"backdrop",preferThumb:!0,inheritThumb:!1,scalable:!0}),Promise.resolve()},function(){return Promise.resolve()})}function loadLatest(){for(var sections=view.querySelectorAll(".latestSection"),promises=[],i=0,length=sections.length;i<length;i++)promises.push(loadLatestSection(sections[i]));return Promise.all(promises)}function renderLocalFolders(parentElement,items,serverName){var html='<div class="verticalSection padded-left padded-right">';html+='<h2 class="sectionTitle">'+(serverName||"Server")+"</h2>";var id="section"+(new Date).getTime();html+='<div id="'+id+'" is="emby-itemscontainer" class="itemsContainer vertical-wrap"></div>',html+="</div>",parentElement.insertAdjacentHTML("beforeend",html),cardBuilder.buildCards(items,{itemsContainer:parentElement.querySelector("#"+id),shape:"backdrop",preferThumb:!0,scalable:!0})}function loadServerFolders(parentElement,apiClient){return apiClient.getLocalFolders().then(function(items){return items.length&&renderLocalFolders(parentElement,items,apiClient.serverName()),Promise.resolve()})}function loadAllServerFolders(){var offlineServers=view.querySelector(".offlineServers");offlineServers.innerHTML="";var promises=connectionManager.getApiClients().map(function(apiClient){return loadServerFolders(offlineServers,apiClient)});return Promise.all(promises)}function loadOfflineCategories(){var promises=[];return promises.push(loadLatest()),promises.push(loadAllServerFolders()),Promise.all(promises)}function autoFocus(){focusManager.autoFocus(view)}view.addEventListener("viewshow",function(e){Emby.Page.setTitle(globalize.translate("sharedcomponents#Downloads"));var isRestored=e.detail.isRestored;isRestored||loadOfflineCategories().then(autoFocus)})}});